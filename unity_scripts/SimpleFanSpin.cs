using UnityEngine;

public class SimpleFanSpin : MonoBehaviour
{
    public float spinSpeed = 300f;
    private bool isSpinning = false;

    void Update()
    {
        if (isSpinning && transform != null)
        {
            transform.Rotate(0, spinSpeed * Time.deltaTime, 0);
        }
    }

    public void StartSpinning()
    {
        isSpinning = true;
    }

    public void StopSpinning()
    {
        isSpinning = false;
    }
}