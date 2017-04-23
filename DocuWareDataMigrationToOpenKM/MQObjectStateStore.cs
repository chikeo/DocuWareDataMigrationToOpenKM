using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Messaging;
using com.openkm.ws;

namespace DocuWareDataMigrationToOpenKM
{
    [Serializable]
    public class MQObjectStateStore
    {
        MessageQueue pQueue;

        public MessageQueue PQueue
        {
            get { return pQueue; }
            set { pQueue = value; }
        }
        string dynamicDocPath;

        public string DynamicDocPath
        {
            get { return dynamicDocPath; }
            set { dynamicDocPath = value; }
        }
        string host = string.Empty;

        public string Host
        {
            get { return host; }
            set { host = value; }
        }
        string user = string.Empty;

        public string User
        {
            get { return user; }
            set { user = value; }
        }
        string password = string.Empty;

        public string Password
        {
            get { return password; }
            set { password = value; }
        }
        string token = string.Empty;

        public string Token
        {
            get { return token; }
            set { token = value; }
        }
        OKMWebservice webservice = null;

        public OKMWebservice Webservice
        {
            get { return webservice; }
            set { webservice = value; }
        }

        
    }
}
