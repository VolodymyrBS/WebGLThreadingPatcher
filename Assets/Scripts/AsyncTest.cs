using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace WebGLThreadingPatcher
{
    public class AsyncTest : MonoBehaviour
    {
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
        }

        private void Update()
        {
            tcs.TrySetResult(null);
            tcs2.TrySetResult(null);
        }
    }
}