using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace WebGLThreadingPatcher
{
    public class AsyncTest : MonoBehaviour
    {
        [System.Runtime.InteropServices.DllImport("__Interlan")]
        private static extern void Do();

        TaskCompletionSource<object> tcs;
        TaskCompletionSource<object> tcs2;

        private async void Start()
        {
            tcs = new TaskCompletionSource<object>();
            tcs2 = new TaskCompletionSource<object>();

            _ = tcs.Task.ContinueWith(t => Debug.LogError("Continue With Done"));

            var t = Task.Run(() => Debug.LogError("Task Run Done"));

            await tcs.Task;
            Debug.LogError("Await Done");

            await tcs2.Task.ConfigureAwait(false);

            Debug.LogError("Await with ConfigureAwait(false) Done");

            await Task.Delay(1000);

            Debug.LogError("Task.Delay done");

            _ = Task.Delay(300).ContinueWith(t => Debug.LogError("Timer Continue With 2 Done"));
            _ = Task.Delay(1500).ContinueWith(t => Debug.LogError("Timer Continue With 3 Done"));
            _ = Task.Delay(800).ContinueWith(t => Debug.LogError("Timer Continue With 4 Done"));
            _ = Task.Delay(10).ContinueWith(t => Debug.LogError("Timer Continue With 5 Done"));
        }

        private void Update()
        {
            tcs.TrySetResult(null);
            tcs2.TrySetResult(null);
        }
    }
}