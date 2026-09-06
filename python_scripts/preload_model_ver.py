import os
import sys
import time
import requests
import numpy as np
import tensorflow as tf
from datetime import datetime

DATABASE_URL = ""
if getattr(sys, "frozen", False):
    BASE_DIR = os.path.dirname(sys.executable)
else:
    BASE_DIR = os.path.dirname(os.path.abspath(__file__))

MODEL_PATH = os.path.join(BASE_DIR, "attendance_model.keras")

WEEKDAY_MAP = {
    "MON": "MON", "MONDAY": "MON",
    "TUE": "TUE", "TUESDAY": "TUE",
    "WED": "WED", "WEDNESDAY": "WED",
    "THU": "THU", "THURSDAY": "THU",
    "FRI": "FRI", "FRIDAY": "FRI",
    "SAT": "SAT", "SATURDAY": "SAT",
    "SUN": "SUN", "SUNDAY": "SUN",
}

DT_TO_WEEKDAY = {
    0: "MON",
    1: "TUE",
    2: "WED",
    3: "THU",
    4: "FRI",
    5: "SAT",
    6: "SUN",
}


def fetch_json(path):
    r = requests.get(f"{DATABASE_URL}/{path}.json", timeout=15)
    r.raise_for_status()
    data = r.json()
    return data if data is not None else {}


def patch_json(path, payload):
    r = requests.patch(f"{DATABASE_URL}/{path}.json", json=payload, timeout=15)
    r.raise_for_status()
    return r.json() if r.text else None


def normalize_weekday(value):
    if value is None:
        return None

    if isinstance(value, int):
        return DT_TO_WEEKDAY.get(value)

    s = str(value).strip().upper()
    if not s:
        return None

    return WEEKDAY_MAP.get(s, s[:3])


def get_next_weekday():
    """
    Priority:
    1) track/nextWeekday
    2) current/nextWeekday
    3) current/weekday
    4) today
    """
    try:
        track = fetch_json("track")
        if isinstance(track, dict):
            wd = normalize_weekday(track.get("nextWeekday"))
            if wd:
                return wd
    except Exception:
        pass

    try:
        current = fetch_json("current")
        if isinstance(current, dict):
            wd = normalize_weekday(current.get("nextWeekday"))
            if wd:
                return wd
            wd = normalize_weekday(current.get("weekday"))
            if wd:
                return wd
    except Exception:
        pass

    return DT_TO_WEEKDAY[datetime.now().weekday()]


def get_student_features(sessions, student_key, target_weekday=None):
    total_sessions = 0
    attended_sessions = 0

    weekday_sessions = 0
    weekday_attended = 0

    seat_sum = 0.0
    seat_count = 0

    for session in sessions.values():
        sess_students = session.get("students", {})
        sdata = sess_students.get(student_key)
        if not sdata:
            continue

        attended = bool(sdata.get("attended"))
        seat_val = sdata.get("seat", 0) or 0

        total_sessions += 1
        if attended:
            attended_sessions += 1

        if seat_val > 0:
            seat_sum += float(seat_val)
            seat_count += 1

        sess_weekday = normalize_weekday(session.get("weekday"))
        if target_weekday is not None and sess_weekday == target_weekday:
            weekday_sessions += 1
            if attended:
                weekday_attended += 1

    overall_avg = attended_sessions / total_sessions if total_sessions > 0 else 0.0

    if target_weekday is None:
        weekday_avg = overall_avg
    else:
        weekday_avg = (
            weekday_attended / weekday_sessions
            if weekday_sessions > 0
            else overall_avg
        )

    average_seat = seat_sum / seat_count if seat_count > 0 else 0.0

    return overall_avg, weekday_avg, average_seat


def assign_sequentially(students):
    student_keys = list(students.keys())
    seat_map = {}
    for i, sk in enumerate(student_keys, start=1):
        seat_map[sk] = i
    return seat_map


def assign_by_model(students, sessions, model, target_weekday):
    student_keys = list(students.keys())
    X = []
    avg_seats = []
    overall_avgs = []
    weekday_avgs = []

    for sk in student_keys:
        overall_avg, weekday_avg, avg_seat = get_student_features(
            sessions,
            sk,
            target_weekday=target_weekday
        )

        X.append([overall_avg, weekday_avg])
        avg_seats.append(avg_seat)
        overall_avgs.append(overall_avg)
        weekday_avgs.append(weekday_avg)

    X = np.array(X, dtype=np.float32)
    probs = model.predict(X, verbose=0).reshape(-1)

    avg_seats = np.array(avg_seats, dtype=np.float32)

    # Priority = model predicted value + 1 / average seat
    # Smaller average seat (closer to the front) gives a larger bonus.
    seat_bonus = np.where(avg_seats > 0, (avg_seats - 1) / 286, 0.0)
    priority = probs + seat_bonus

    order = np.argsort(-priority)

    seat_map = {}
    for rank, idx in enumerate(order, start=1):
        seat_map[student_keys[idx]] = rank

    return seat_map, probs, avg_seats, priority, student_keys, overall_avgs, weekday_avgs


def main():
    t0 = time.perf_counter()

    students = fetch_json("students") or {}
    sessions = fetch_json("sessions") or {}

    if not students:
        print("No students found in database.")
        input("Press Enter to close...")
        return

    if not sessions:
        print("No previous session data found. Assigning sequential seats...")
        seat_map = assign_sequentially(students)

        changed = sum(
            1 for sk, new_seat in seat_map.items()
            if int(students.get(sk, {}).get("seat", 0) or 0) != new_seat
        )

        payload = {sk: {"seat": seat_map[sk]} for sk in seat_map}
        patch_json("students", payload)

        elapsed = time.perf_counter() - t0
        print(f"Seats assigned sequentially in {elapsed:.3f} seconds.")
        print(f"Seats changed: {changed}/{len(students)}")
        input("Press Enter to close...")
        return

    if not os.path.exists(MODEL_PATH):
        print(f"Model file not found: {MODEL_PATH}")
        print("Train and save the model first, then run deployment again.")
        input("Press Enter to close...")
        return

    print("Looking for model at:", MODEL_PATH)
    model = tf.keras.models.load_model(MODEL_PATH)

    target_weekday = get_next_weekday()
    print(f"Target weekday for next session: {target_weekday}")

    seat_map, probs, avg_seats, priority, student_keys, overall_avgs, weekday_avgs = assign_by_model(
        students,
        sessions,
        model,
        target_weekday
    )

    changed = 0
    for sk in student_keys:
        old_seat = int(students.get(sk, {}).get("seat", 0) or 0)
        new_seat = int(seat_map[sk])
        if old_seat != new_seat:
            changed += 1

    for sk in seat_map:
        patch_json(f"students/{sk}", {"seat": int(seat_map[sk])})

    elapsed = time.perf_counter() - t0

    print(f"Seats assigned in {elapsed:.3f} seconds.")
    print(f"Seats changed: {changed}/{len(student_keys)}")

    ordered = sorted(
        [
            (
                sk,
                int(students.get(sk, {}).get("seat", 0) or 0),
                seat_map[sk],
                float(overall_avgs[i]),
                float(weekday_avgs[i]),
                float(probs[i]),
                float(avg_seats[i]),
                float(priority[i])
            )
            for i, sk in enumerate(student_keys)
        ],
        key=lambda x: x[2]
    )

    print("\nFinal seat order:")
    for sk, old_seat, new_seat, overall_avg, weekday_avg, prob, avg_seat, prio in ordered:
        name = students.get(sk, {}).get("name", sk)
        print(
            f"Seat {old_seat:03d} -> {new_seat:03d} | {name} | "
            f"overall_avg={overall_avg:.4f} | "
            f"weekday_avg={weekday_avg:.4f} | "
            f"p(attend)={prob:.4f} | "
            f"avg_seat={avg_seat:.2f} | "
            f"priority={prio:.4f}"
        )

    input("\nPress Enter to close...")


if __name__ == "__main__":
    main()
