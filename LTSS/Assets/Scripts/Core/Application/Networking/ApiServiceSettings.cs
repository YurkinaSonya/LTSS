using System;
using System.Collections.Generic;

namespace Game.Core.Application.Networking
{
    [Serializable]
    public sealed class ApiServiceSettings
    {
        public string BaseUrl = "https://tomcat.sonya.jij.li/ltss";
        public int TimeoutSeconds = 10;
        public List<ApiHeaderEntry> DefaultHeaders = new List<ApiHeaderEntry>();
    }

    [Serializable]
    public sealed class ApiHeaderEntry
    {
        public string Key;
        public string Value;
    }
}
