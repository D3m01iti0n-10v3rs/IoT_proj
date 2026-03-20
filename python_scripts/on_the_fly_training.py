import requests
import tensorflow as tf
import numpy as np

DATABASE_URL = "https://test-119a8-default-rtdb.asia-southeast1.firebasedatabase.app"


def fetch_json(path):
    return requests.get(f"{DATABASE_URL}/{path}.json").json()


def patch_json(path, payload):
    requests.patch(f"{DATABASE_URL}/{path}.json", json=payload)


def main():

    sessions = fetch_json("sessions")
    students = fetch_json("students")

    student_keys = list(students.keys())
    print(f"Students: {len(student_keys)}")

    # --------------------------------------------------
    # No historical data
    # --------------------------------------------------
    if not sessions:
        print("Assigning seats linearly")

        for i, student_key in enumerate(student_keys):
            seat = i + 1
            patch_json(f"students/{student_key}", {"seat": seat})

        print("Linear seat assigned")
        return

    # --------------------------------------------------
    # Process historical data
    # --------------------------------------------------
    attendance_count = {}
    front_seat_score = {}

    for session in sessions.values():
        for student_key, data in session["students"].items():

            if student_key not in attendance_count:
                attendance_count[student_key] = 0
                front_seat_score[student_key] = 0.0

            if data["attended"]:
                attendance_count[student_key] += 1

            seat_value = data["seat"]
            front_seat_score[student_key] += (1.0 / seat_value)

    attendance_tensor = tf.constant(
        [attendance_count.get(k, 0) for k in student_keys],
        dtype=tf.float32
    )

    front_tensor = tf.constant(
        [front_seat_score.get(k, 0.0) for k in student_keys],
        dtype=tf.float32
    )

    # priority = attendance - front_seat_score
    inputs = tf.stack([attendance_tensor, front_tensor], axis=1)

    model = tf.keras.Sequential([
        tf.keras.layers.Dense(1, use_bias=False)
    ])

    model.build((None, 2))
    model.layers[0].set_weights([
        np.array([[1.0], [-1.0]])
    ])

    priority_scores = tf.squeeze(model(inputs))

    sorted_indices = tf.argsort(priority_scores, direction="DESCENDING").numpy()

    for rank, idx in enumerate(sorted_indices):
        if rank >= len(student_keys):
            break

        student_key = student_keys[idx]
        seat = rank + 1
        patch_json(f"students/{student_key}", {"seat": seat})

    print("Priority-based seat assignment completed.")


if __name__ == "__main__":
    main()
