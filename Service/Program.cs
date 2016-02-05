using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Open.Nat;

namespace Service
{
    public static class Program
    {
        public static void Main()
        {
            ServiceRunner r = new ServiceRunner();
        }
    }
}