using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace StreamingClient
{
    public partial class App 
    {
        void AppStartup(object sender, StartupEventArgs args)
        {
            MainWindow mainWindow = new MainWindow();
            mainWindow.Show();
        }
    }
}