using System;
using System.Collections.Generic;
using Runtime;
using Steamworks;

namespace SteamToys.Runtime.Core
{
    [Serializable]
    public struct ProjectAppIdRef
    {
        public string ProjectName;
        public AppId_t AppId;

        public ProjectAppIdRef(string projectName, AppId_t appId)
        {
            ProjectName =  projectName;
            AppId = appId;
        }
    }
    
    public class SteamSettings : SingletonObject<SteamSettings>
    {
        [ProjectAppId] public AppId_t AppId;

        public List<ProjectAppIdRef> PosibleAppIds = new() {
            new ProjectAppIdRef("Spacewar", new AppId_t(480))
        };
    }
}