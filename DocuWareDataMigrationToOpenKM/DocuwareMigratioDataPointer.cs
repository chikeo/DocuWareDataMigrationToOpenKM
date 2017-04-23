using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EnqueueDocuwareDataForMigrationToOpenKM
{
    [Serializable]
    public class DocuwareMigrationDataPointer
    {
        string path;

        public string Path
        {
            get { return path; }
            set { path = value; }
        }
        Dictionary<string, string> metadata;

        public Dictionary<string, string> Metadata
        {
            get { return metadata; }
            set { metadata = value; }
        }
        string transformedPath;

        public string TransformedPath
        {
            get { return transformedPath; }
            set { transformedPath = value; }
        }

    }
}
