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
		public static ILogger GetInstance () { return Context.Resolve<ILogger>(); } 

		public static void Warning(string message)
		{
			GetInstance().Warning (message);
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
	public class ExpectationLogger : ILogger
	{
		private string expectation;

		public ExpectationLogger(string expectation)
		{
			this.expectation = expectation;
		}

		public void Warning (string message)
		{
			if (message != expectation)
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
			Context.Create (() => {

				Context.Register<ILogger>(new ConsoleLogger());

				InjectedLoggerMethod("from first context");

				//Using a subcontext with new service definition
				Context.Create(() => {

					InjectedLoggerMethod("before override service");

					Context.Register<ILogger>(new ExpectationLogger("expect"));

					InjectedLoggerMethod("expect");

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
