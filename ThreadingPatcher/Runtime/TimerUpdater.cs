using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;

[assembly: AlwaysLinkAssembly]

namespace WebGLThreadingPatcher.Runtime
{
    [Preserve]
    public class TimerRunner : MonoBehaviour
    {
        private object _timerSchedulerInstance;
        private MethodInfo _timerSchedulerLoop;

        [Preserve]
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            var go = new GameObject(nameof(TimerRunner));
            go.AddComponent<TimerRunner>();

            DontDestroyOnLoad(go);
        }

        [Preserve]
        private void Awake()
        {
            var timer = typeof(System.Threading.Timer);
            var scheduler = timer.GetNestedType("Scheduler", BindingFlags.NonPublic);

            _timerSchedulerLoop = scheduler.GetMethod("RunSchedulerLoop", BindingFlags.Instance | BindingFlags.NonPublic);
            _timerSchedulerInstance = scheduler.GetProperty("Instance").GetValue(null);
        }

        [Preserve]
        private void Start()
        {
#if UNITY_2021_2_OR_NEWER
            StartCoroutine(TimerUpdateCoroutine());
#endif
        }

        private IEnumerator TimerUpdateCoroutine()
        {
#if UNITY_EDITOR
            yield break;
#endif
#pragma warning disable CS0162 // Unreachable code detected
            while (true)
            {
                var delay = (int)_timerSchedulerLoop.Invoke(_timerSchedulerInstance, null);
                if (delay == -1)
                    yield return null;
                else
                    yield return new WaitForSeconds(delay / 1000);
            }
#pragma warning restore CS0162 // Unreachable code detected
        }
    }
}