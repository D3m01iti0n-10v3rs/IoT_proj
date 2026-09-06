import requests
import random
import time
from datetime import datetime

DB_URL = ""

ENTER_DELAY = (0.01, 0.05)
LEAVE_DELAY = (0.01, 0.05)

MIN_ATTEND_RATIO = 0.25
MAX_ATTEND_RATIO = 1

def db_get(path):
    r = requests.get(f"{DB_URL}/{path}.json")
    return r.json()

def db_put(path, value):
    requests.put(f"{DB_URL}/{path}.json", json=value)

def db_patch(path, value):
    requests.patch(f"{DB_URL}/{path}.json", json=value)

def simulate_class():
    students = db_get("students")
    student_keys = list(students.keys())

    total_students = len(student_keys)

    attend_ratio = random.uniform(MIN_ATTEND_RATIO, MAX_ATTEND_RATIO)
    attend_count = int(total_students * attend_ratio)

    attending_students = random.sample(student_keys, attend_count)

    random.shuffle(attending_students)

    current_count = 0
    present = []

    print(f"Total students: {total_students}")
    print(f"Attending today: {attend_count}\n")

    for key in attending_students:
        time.sleep(random.uniform(*ENTER_DELAY))

        now = datetime.now().strftime("%H:%M:%S %d-%m-%Y")
        db_patch(f"students/{key}", {
            "attendance_flag": True,
            "time_of_attendance": now
        })

        present.append(key)
        current_count += 1
        db_put("track/studentNumber", current_count)

        print(f"Entering: {key} - {current_count}")

    time.sleep(1)

    random.shuffle(present)

    for key in present:
        time.sleep(random.uniform(*LEAVE_DELAY))

        db_patch(f"students/{key}", {
            "attendance_flag": False,
            "time_of_attendance": "n/a"
        })

        current_count -= 1
        db_put("track/studentNumber", current_count)

        print(f"Leaving: {key} - {current_count}")


if __name__ == "__main__":
    simulate_class()
