using System;
using NStack;
using Services;
using System.Threading;

namespace Services
{
	//Service description
	public interface ILogger
	{
		void Warning(string message);
	}

	//Service helper (optional)
	public static class Logger
	{
		public static ILogger Current { get { return Context.Resolve<ILogger> (); } } 

		public static void Warning(string message)
		{
			Current.Warning (message);
		}
	}

	//Service implementations

	//Implementarion over console
	public class ConsoleLogger : ILogger
	{
		public void Warning (string message)
		{
			Console.WriteLine("[warning - {0}]: {1}", DateTime.Now, message);
		}
	}

	//Fake implementation with parameter expectation check
	public class VoidLogger : ILogger
	{
		public void Warning (string message)
		{
			if (message != "void")
			{
				throw new Exception ("expectation exception");
			}
		}
	}
}

namespace NStack.Example
{
	class MainClass
	{
		public static void Main (string[] args)
		{

			//Exceptions
			try
			{
				Context.Resolve<int> ();
			}
			catch(Context.IrresolvableServiceException ex)
			{
				Console.WriteLine(ex.Message);
			}

			Context.Create (() => {

				try
				{
					Context.Resolve<int> ();
				}
				catch(Context.IrresolvableServiceException ex)
				{
					Console.WriteLine(ex.Message);
				}

				Context.Register<ILogger>(new ConsoleLogger());


				try
				{
					Context.Register<ILogger>(new ConsoleLogger());
				}
				catch(Context.DuplicatedServiceRegistrationException ex)
				{
					Console.WriteLine(ex.Message);
				}


				InjectedLoggerMethod("from first context");

				//Using a subcontext with new service definition
				Context.Create(() => {

					InjectedLoggerMethod("before override service");

					Context.Register<ILogger>(new VoidLogger());

					InjectedLoggerMethod("void");

				});

				//Preserving context on new thread
				new Thread(
					new ThreadStart(
						Context.Save(() => {

							InjectedLoggerMethod("from new thread");

						})
					)
				).Start();

			});
		
		}

		static void InjectedLoggerMethod(string message)
		{
			Logger.Warning (message);
		}
	}
}
