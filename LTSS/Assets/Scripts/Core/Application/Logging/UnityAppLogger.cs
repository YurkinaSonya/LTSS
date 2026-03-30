using System;
using UnityEngine;

namespace Game.Core.Application.Logging
{
    public sealed class UnityAppLogger : IAppLogger
    {
        public void Info(string message)
        {
            Debug.Log($"[App] {message}");
        }

        public void Warning(string message)
        {
            Debug.LogWarning($"[App] {message}");
        }

        public void Error(string message, Exception exception = null)
        {
            if (exception == null)
            {
                Debug.LogError($"[App] {message}");
                return;
            }

            Debug.LogError($"[App] {message}\n{exception}");
        }
    }
}
