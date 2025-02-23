﻿using System.Collections.Generic;
using RevEng.Common;

namespace EFCorePowerTools.Helpers
{
    public class NuGetPackage
    {
        public string PackageId { get; set; }
        public string Version { get; set; }
        public string UseMethodName { get; set; }
        public bool Installed { get; set; }
        public bool IsMainProviderPackage { get; set; }
        public List<DatabaseType> DatabaseTypes { get; set; }
    }
}
