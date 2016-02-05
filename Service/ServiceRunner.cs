using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Metadata.W3cXsd2001;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Open.Nat;

namespace Service
{
    public class ServiceRunner : IDisposable
    {
        private static ServiceHost myServiceHost = null;
        private static NatDiscoverer discoverer = null;
        private bool ServiceIsReady = false;
        private List<NatDevice> devices = null;
        private string externalIP = "";
        private string internalIP = "";
        private int _portNumber = 0; 
        private NatDevice primaryDevice = null;
        bool disposed;


        public ServiceRunner()
        {
            externalIP = Properties.Settings.Default.IPAddress ?? "192.168.1.10";
            _portNumber = Properties.Settings.Default.PortNumber;
            
        }
        public async Task<bool> DiscoverDevices()
        {
            try
            {
                discoverer = new NatDiscoverer();
                var cts = new CancellationTokenSource(5000);
                devices = (await discoverer.DiscoverDevicesAsync(PortMapper.Upnp, cts)).ToList();
            }
            catch (Exception)
            {
                throw;
                // log?
            }
            
            return true;
        }

        public async Task<string> GetExternalIP()
        {
            foreach (var natDevice in devices)
            {
                string i = natDevice.GetExternalIPAsync().Result.ToString();
                if (i == "0.0.0.0") continue;
                this.primaryDevice = natDevice;
                break;
            }
            var ip = await primaryDevice.GetExternalIPAsync();
            externalIP = ip.ToString();
            return externalIP;
        }

        public async Task<bool> CheckIfPortForwardingExists()
        {
            
            var a = await primaryDevice.GetAllMappingsAsync();
            if (a.Any(mapping => mapping.PrivatePort == _portNumber))
            {
                // already created
                return true;
            }

            await primaryDevice.CreatePortMapAsync(new Mapping(Protocol.Tcp, _portNumber, _portNumber, 0, "AudioStream"));
            if (a.Any(mapping => mapping.PrivatePort == _portNumber))
            {
                return true;
            }
            return false;
        }

        

        public async void StartService(int port)
        {

            try
            {
                ModifyHttpSettings();
                await DiscoverDevices();
                await GetExternalIP();
                internalIP = await GetInternalIP();
                await CheckIfPortForwardingExists();
                StartHost();
                Console.WriteLine("Service is started.");
                Console.WriteLine("Press any key to stop it.");
                Console.ReadKey();
            }
            catch (AddressAccessDeniedException)
            {
                Console.WriteLine("register listener port with netsh.exe");
                throw;
            }
            catch (Exception e)
            {
                string mess = e.Message;
                Console.WriteLine(e.Message);
            }
        }

        public string StartHost()
        {
            if (string.IsNullOrEmpty(internalIP))
            {
                internalIP = GetInternalIP().Result;
            }
            ModifyHttpSettings();
            Uri baseAddress = new Uri("http://" + internalIP + ":" + _portNumber);
            myServiceHost = new ServiceHost(typeof (Mp3StreamingService), baseAddress);
            
            var binding = new WebHttpBinding();
            var endpoint = myServiceHost.AddServiceEndpoint(typeof (IStreamingService), binding, "");
            endpoint.EndpointBehaviors.Add(new WebHttpBehavior());
            myServiceHost.Open();

            int timer = 0;
            while (myServiceHost.State != CommunicationState.Opened)
            {
                Thread.Sleep(300);
                timer += 300;
                if (timer >= 90000)
                    throw new Exception("Could not start service host.");
            }

            Properties.Settings.Default.IPAddress = externalIP;
            Properties.Settings.Default.Save();

            return externalIP + ":" + _portNumber;
        }

        private async Task<string> GetInternalIP()
        {
            IPHostEntry host;
            string localIP = "?";
            host = await Task.Run(() =>  Dns.GetHostEntry(Dns.GetHostName()));
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily.ToString() == "InterNetwork")
                {
                    localIP = ip.ToString();
                }
            }
            return localIP;
        }


        public void StopService()
        {
            if (myServiceHost != null && myServiceHost.State == CommunicationState.Opened)
            {
                myServiceHost.Close();
            }
            myServiceHost = null;
            //primaryDevice.DeletePortMapAsync(primaryDevice.GetSpecificMappingAsync(Protocol.Tcp, 54000).Result);
        }

        public void ModifyHttpSettings()
        {
            string everyone = new System.Security.Principal.SecurityIdentifier(
                "S-1-1-0").Translate(typeof(System.Security.Principal.NTAccount)).ToString();

            string parameter = $@"http add urlacl url=http://+:{_portNumber}/ user=\everyone";

            ProcessStartInfo psi = new ProcessStartInfo("netsh", parameter);

            psi.Verb = "runas";
            psi.RedirectStandardOutput = false;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
            psi.UseShellExecute = true;
            Process.Start(psi);
        }

        public async Task<bool> RequestFirewallRule()
        {
            var fwManager = new FirewallHelper();
            string title = "";
            Assembly currentAssem = typeof(ServiceRunner).Assembly;
            object[] attribs = currentAssem.GetCustomAttributes(typeof(AssemblyTitleAttribute), true);
            if (attribs.Length > 0)
            {
                title = ((AssemblyTitleAttribute)attribs[0]).Title;
            }


            var path = Assembly.GetExecutingAssembly().Location;
            return await fwManager.openFirewall(title, path, _portNumber);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    StopService();
                    var fw = new FirewallHelper();
                    var path = Assembly.GetExecutingAssembly().Location;
                    fw.closeFirewall(path,_portNumber);
                }
            }
            //dispose unmanaged resources
            disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}

