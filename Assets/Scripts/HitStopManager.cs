using System.Collections;
using UnityEngine;

public sealed class HitStopManager : MonoBehaviour
{
    private static HitStopManager instance;
    private Coroutine routine;

    public static void Request(float duration, float timeScale)
    {
        if (duration <= 0f)
        {
            return;
        }

        Instance.Play(duration, timeScale);
    }

    private static HitStopManager Instance
    {
        get
        {
            if (instance != null)
            {
                return instance;
            }

            instance = FindObjectOfType<HitStopManager>();
            if (instance != null)
            {
                return instance;
            }

            GameObject managerObject = new GameObject("HitStopManager");
            instance = managerObject.AddComponent<HitStopManager>();
            return instance;
        }
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Play(float duration, float timeScale)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            Time.timeScale = 1f;
        }

        routine = StartCoroutine(Routine(duration, timeScale));
    }

    private IEnumerator Routine(float duration, float timeScale)
    {
        float previousScale = Time.timeScale;
        Time.timeScale = Mathf.Clamp01(timeScale);
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = previousScale;
        routine = null;
    }
}
