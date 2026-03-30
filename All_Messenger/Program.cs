using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System;

namespace All_Messenger;

public static class Program
{
  [STAThread]
  static void Main(string[] args)
  {
    // Self-contained WinAppSDK unpackaged: không cần DeploymentManager hay Bootstrap.
    // Tất cả DLL đã được bundle sẵn trong publish folder.
    WinRT.ComWrappersSupport.InitializeComWrappers();
    Application.Start((p) =>
    {
      var context = new DispatcherQueueSynchronizationContext(
              DispatcherQueue.GetForCurrentThread());
      System.Threading.SynchronizationContext.SetSynchronizationContext(context);
      _ = new App();
    });
  }
}
