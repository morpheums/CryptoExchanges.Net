using Ninject;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace CryptoExchanges.Net.DI
{
    public class Kernel
    {
        private static Kernel _kernel;
        private static IKernel _ninjectKernel;

        private Kernel()
        {
            _ninjectKernel = new StandardKernel();
            _ninjectKernel.Load(LoadAssembliesFromCurrentFolder());
        }

        public static Kernel Instance
        {
            get
            {
                if (_kernel == null)
                {
                    _kernel = new Kernel();
                }

                return _kernel;
            }
        }

        public IKernel NinjectKernel => _ninjectKernel;

        private List<Assembly> LoadAssembliesFromCurrentFolder()
        {
            var allAssemblies = new List<Assembly>();
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            foreach (string dll in Directory.GetFiles(path, "CryptoExchanges.Net*.dll"))
            {
                allAssemblies.Add(Assembly.LoadFile(dll));
            }

            return allAssemblies;
        }

        private void Test()
        {
            //_ninjectKernel.Bind(x => x
            //        .FromThisAssembly()
            //        .IncludingNonePublicTypes()
            //        .SelectAllClasses()
            //        .InheritedFrom(typeof(ICredentialsProvider<>))
            //        .BindDefaultInterface()
            //        .Configure(y => y.InRequestScope()));

            //_ninjectKernel.Bind(x => x
            //.FromAssembliesMatching("*")
            //.SelectAllClasses()
            //.InheritedFrom(typeof(IRepository<>))
            //.BindDefaultInterface());
        }
    }
}
