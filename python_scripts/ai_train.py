import os
import numpy as np
import tensorflow as tf
import matplotlib.pyplot as plt

MODEL_PATH = "attendance_model.keras"
RANDOM_SEED = 42


def make_synthetic_dataset(n_samples=20000, seed=RANDOM_SEED):
    rng = np.random.default_rng(seed)

    # Inputs in [0, 1]
    overall_avg = rng.random(n_samples).astype(np.float32)
    weekday_avg = rng.random(n_samples).astype(np.float32)

    X = np.column_stack([overall_avg, weekday_avg]).astype(np.float32)

    # Stronger relationship, less noise -> easier classification
    noise = rng.normal(0.0, 0.7, n_samples).astype(np.float32)

    latent = (
        6.0 * overall_avg
        + 7.0 * weekday_avg
        - 4.5
        + noise
    )

    p_attend = 1.0 / (1.0 + np.exp(-latent))

    # Binary label sampled from probability
    y = (0.3 + (0.7 - 0.3) * rng.random(n_samples) < p_attend).astype(np.float32)

    return X, y


def build_model():
    model = tf.keras.Sequential([
        tf.keras.layers.Input(shape=(2,)),
        tf.keras.layers.Dense(16, activation="relu"),
        tf.keras.layers.Dense(16, activation="relu"),
        tf.keras.layers.Dense(8, activation="relu"),
        tf.keras.layers.Dense(1, activation="sigmoid"),
    ])

    model.compile(
        optimizer=tf.keras.optimizers.Adam(learning_rate=0.0001),
        loss="binary_crossentropy",
        metrics=["accuracy"]
    )
    return model


class EpochPrinter(tf.keras.callbacks.Callback):
    def on_epoch_end(self, epoch, logs=None):
        logs = logs or {}
        print(
            f"Epoch {epoch + 1:03d} | "
            f"loss={logs.get('loss', 0):.4f} | "
            f"acc={logs.get('accuracy', 0):.4f} | "
            f"val_loss={logs.get('val_loss', 0):.4f} | "
            f"val_acc={logs.get('val_accuracy', 0):.4f}"
        )


def plot_dataset(X, y, title="Dataset"):
    plt.figure(figsize=(8, 6))
    plt.scatter(X[:, 0], X[:, 1], c=y, cmap="coolwarm", s=12, alpha=0.7)
    plt.xlabel("overall_avg")
    plt.ylabel("weekday_avg")
    plt.title(title)
    plt.colorbar(label="attendance label")
    plt.grid(True, alpha=0.3)
    plt.show()


def plot_training_history(history):
    epochs = range(1, len(history.history["loss"]) + 1)

    plt.figure(figsize=(8, 5))
    plt.plot(epochs, history.history["loss"], label="Training Loss")
    plt.plot(epochs, history.history["val_loss"], label="Validation Loss")
    plt.xlabel("Epoch")
    plt.ylabel("Loss")
    plt.title("Loss over Epochs")
    plt.legend()
    plt.grid(True, alpha=0.3)
    plt.show()

    plt.figure(figsize=(8, 5))
    plt.plot(epochs, history.history["accuracy"], label="Training Accuracy")
    plt.plot(epochs, history.history["val_accuracy"], label="Validation Accuracy")
    plt.xlabel("Epoch")
    plt.ylabel("Accuracy")
    plt.title("Accuracy over Epochs")
    plt.legend()
    plt.grid(True, alpha=0.3)
    plt.show()


def main():
    tf.random.set_seed(RANDOM_SEED)
    np.random.seed(RANDOM_SEED)

    X, y = make_synthetic_dataset()

    idx = np.random.permutation(len(X))
    X = X[idx]
    y = y[idx]

    n = len(X)
    n_train = int(0.8 * n)
    n_val = int(0.1 * n)

    X_train, y_train = X[:n_train], y[:n_train]
    X_val, y_val = X[n_train:n_train + n_val], y[n_train:n_train + n_val]
    X_test, y_test = X[n_train + n_val:], y[n_train + n_val:]

    plot_dataset(X_train, y_train, "Training Dataset")
    plot_dataset(X_val, y_val, "Validation Dataset")

    model = build_model()

    callbacks = [
        EpochPrinter(),
        tf.keras.callbacks.ReduceLROnPlateau(
            monitor="val_loss",
            factor=0.5,
            patience=4,
            min_lr=1e-5
        ),
        tf.keras.callbacks.ModelCheckpoint(
            MODEL_PATH,
            monitor="val_accuracy",
            save_best_only=True,
            save_weights_only=False,
            verbose=0
        )
    ]

    print("Training model...")
    history = model.fit(
        X_train, y_train,
        validation_data=(X_val, y_val),
        epochs=50,
        batch_size=200,
        verbose=0,
        callbacks=callbacks
    )

    plot_training_history(history)

    print("\nFinal evaluation on test set:")
    test_loss, test_acc = model.evaluate(X_test, y_test, verbose=0)
    print(f"Test loss: {test_loss:.4f}")
    print(f"Test accuracy: {test_acc:.4f}")

    sample_pred = model.predict(X_test[:5], verbose=0).reshape(-1)
    print("\nSample predictions:")
    for i in range(min(5, len(sample_pred))):
        print(f"  x={X_test[i]} -> predicted_attendance_probability={sample_pred[i]:.4f}")

    confirm = input(f"\nSave model to '{MODEL_PATH}'? [y/n]: ").strip().lower()
    if confirm == "y":
        model.save(MODEL_PATH)
        print(f"Model saved to {MODEL_PATH}")
    else:
        print("Model not saved.")


if __name__ == "__main__":
    main()