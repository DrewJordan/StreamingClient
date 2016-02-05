using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NetFwTypeLib;

namespace Service
{
    public class FirewallHelper
    {
        protected INetFwProfile fwProfile;
        private static FirewallHelper _firewallHelper;

        private const string MANAGER = "INetFwMgr";
        private const string PORT = "INetOpenPort";
        private const string APPLICATION = "InetAuthApp";

        protected string _title;
        protected string _path;

        public static FirewallHelper Instance => _firewallHelper ?? (_firewallHelper = new FirewallHelper());

        public async Task<bool> openFirewall(string title, string path, int port)
        {
            _title = title;
            _path = path;
            ///////////// Firewall Authorize Application ////////////
            setProfile();
            INetFwAuthorizedApplications apps = fwProfile.AuthorizedApplications;
            INetFwAuthorizedApplication app = (INetFwAuthorizedApplication)GetInstance(APPLICATION);
            app.Name = title;
            app.ProcessImageFileName = path;
            apps.Add(app);

            //////////////// Open Needed Ports /////////////////
            INetFwOpenPorts openports = fwProfile.GloballyOpenPorts;

            INetFwOpenPort openport = (INetFwOpenPort)GetInstance(PORT);
            openport.Port = port;
            openport.Protocol = NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP;
            openport.Name = "New Open Port";
            openports.Add(openport);

            return await ValidateFirewallChanges(title, port);
        }

        private async Task<bool> ValidateFirewallChanges(string title, int port)
        {
            if (IsAppFound(title) && IsPortFound(port))
            {
                return true;
            }

            return false;
        }

        protected internal bool IsAppFound(string appName)
        {
            bool boolResult = false;
            INetFwAuthorizedApplications apps = null;
            INetFwAuthorizedApplication app = null;
            try
            {
                if (fwProfile.FirewallEnabled)
                {
                    apps = fwProfile.AuthorizedApplications;
                    IEnumerator appEnumerate = apps.GetEnumerator();
                    while ((appEnumerate.MoveNext()))
                    {
                        app = appEnumerate.Current as INetFwAuthorizedApplication;
                        if (app.Name == appName)
                        {
                            boolResult = true;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("An error occurred verifying the firewall rules. See the inner exception for details.", ex);
            }
            return boolResult;
        }

        protected internal bool IsPortFound(int portNumber)
        {
            bool boolResult = false;
            INetFwOpenPorts ports = null;
            INetFwOpenPort currentPort = null;
            try
            {
                ports = fwProfile.GloballyOpenPorts;
                IEnumerator portEnumerate = ports.GetEnumerator();
                while ((portEnumerate.MoveNext()))
                {
                    currentPort = portEnumerate.Current as INetFwOpenPort;
                    if (currentPort.Port == portNumber)
                    {
                        boolResult = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception(
                    "An error occurred while checking if a port was open. Please see the inner exception for details.",
                    ex);
            }
            return boolResult;
        }

        public void closeFirewall(string path, int port)
        {
            setProfile();
            INetFwAuthorizedApplications apps = fwProfile.AuthorizedApplications;
            apps.Remove(path);
            INetFwOpenPorts ports = fwProfile.GloballyOpenPorts;
            ports.Remove(port, NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP);
        }


        protected void setProfile()
        {
            // Access INetFwMgr
            INetFwMgr fwMgr = (INetFwMgr)GetInstance(MANAGER);
            INetFwPolicy fwPolicy = fwMgr.LocalPolicy;
            fwProfile = fwPolicy.CurrentProfile;
        }

        protected object GetInstance(string typeName)
        {
            if (typeName == MANAGER)
            {
                Type type = Type.GetTypeFromCLSID(
                new Guid("{304CE942-6E39-40D8-943A-B913C40C9CD4}"));
                return Activator.CreateInstance(type);
            }
            else if (typeName == APPLICATION)
            {
                Type type = Type.GetTypeFromCLSID(
                new Guid("{EC9846B3-2762-4A6B-A214-6ACB603462D2}"));
                return Activator.CreateInstance(type);
            }
            else if (typeName == PORT)
            {
                Type type = Type.GetTypeFromCLSID(
                new Guid("{0CA545C6-37AD-4A6C-BF92-9F7610067EF5}"));
                return Activator.CreateInstance(type);
            }
            else return null;
        }


    }
}


