using System;
using System.Collections.Generic;
using Runtime;
using SteamToys.Runtime.Core;
using Steamworks;

namespace SteamToys.Runtime
{
    [Serializable]
    public struct ProjectAppIdRef
    {
        public string ProjectName;
        public AppId_t AppId;
    }
    
    public class SteamSettings : SingletonObject<SteamSettings>
    {
        [ProjectAppId] public AppId_t AppId;

        public List<ProjectAppIdRef> PosibleAppIds;
    }
}