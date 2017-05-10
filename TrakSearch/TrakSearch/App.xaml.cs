using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using NLog;

namespace Shravan.DJ.TrakSearch
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{

		private readonly Logger logger = LogManager.GetCurrentClassLogger(); // creates a logger using the class name

		protected override void OnStartup(StartupEventArgs e)
		{
			AppDomain.CurrentDomain.UnhandledException +=
			  new UnhandledExceptionEventHandler(this.AppDomainUnhandledExceptionHandler);

			this.DispatcherUnhandledException +=
			  new DispatcherUnhandledExceptionEventHandler(DispatcherUnhandledExceptionHandler);

			logger.Log(LogLevel.Info, "...Starting App...");
			base.OnStartup(e);
		}

		void AppDomainUnhandledExceptionHandler(object sender, UnhandledExceptionEventArgs ea)
		{
			Exception e = (Exception)ea.ExceptionObject;
			logger.Error(e, "Unhandled AppDomainUnhandledExceptionHandler");
			// log exception

		}

		void DispatcherUnhandledExceptionHandler(object sender, DispatcherUnhandledExceptionEventArgs args)
		{
			// and also:
			logger.Error(args.Exception, "Unhandled DispatcherUnhandledExceptionHandler"); // which will log the stack trace.

			args.Handled = true;
			// implement recovery
		}
	}
}
