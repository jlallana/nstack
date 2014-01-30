using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Emit;


namespace NStack
{
	public class Context
	{
		//Creates a new context
		public static void Create(Action act)
		{
			Create (act, current);
		}

		private static void Create(Action act, Context previous)
		{
			//Make new context
			current = new Context (previous);

			//Invoke context action
			act ();

			//Restore previous context
			current = current.previous;
		}

		//Reference to previous created context on stack
		private Context previous = null;

		//Reference to registred services implementations
		private Dictionary<Type, Func<object>> instances = new Dictionary<Type, Func<object>>();

		//The las context in the current stackcall
		[ThreadStatic] private static Context current;

		//Get the current implementation for the specified service
		public static T Resolve<T>()
		{
			try
			{
				return current.resolve<T> ();
			}
			catch(NullReferenceException)
			{
				throw new IrresolvableServiceException (typeof(T));
			}
		}

		//Instance implementation of resolve
		public T resolve<T>()
		{
			try 
			{
				return (T)this.instances[typeof(T)]();
			}
			catch(KeyNotFoundException)
			{
				return this.previous.resolve<T> ();
			}
		}

		//Preserve the current conext of action for threding
		public static Action Save(Action action)
		{
			var save = current;
			return () => Context.Create(action, save);
		}

		//Preserve the current conext of action for threding
		public static Action<T> Save<T>(Action<T> action)
		{
			var save = current;
			return (param) => Context.Create(() => action(param), save);
		}

		public static void Register<T>(Func<T> constructor)
		{
			try 
			{
				current.instances.Add(typeof(T), () => constructor());
			}
			catch(ArgumentException)
			{
				throw new DuplicatedServiceRegistrationException (typeof(T));
			}
			catch(NullReferenceException)
			{
				throw new OutOfContextException (typeof(T));
			}
		}

		public static void Register<T>(T instance)
		{
			Register<T> (() => instance);
		}

		//Initialization from a context
		private Context(Context previous = null)
		{
			this.previous = previous;
		}

		public class ContextException : Exception
		{
			public ContextException(Type type, string message): base(message)
			{
			}

			public Type TargetService { get; private set; }
		}

		public class OutOfContextException : ContextException
		{
			public OutOfContextException(Type type):base(type, "There is no context defined for the current call.")
			{
			}
		}

		public class DuplicatedServiceRegistrationException : ContextException
		{
			public DuplicatedServiceRegistrationException(Type type) : 
			base(
				type,
				string.Format(
					"The service '{0}' in assembly '{1}' is already registered in the current context.",
					type.FullName,
					type.Assembly.GetName().Name
				)
			)
			{

			}
		}

		public class IrresolvableServiceException : ContextException
		{
			public IrresolvableServiceException(Type type) : 
				base(
				type,
					string.Format(
						"The service '{0}' in assembly '{1}' is not registered in the current context.",
					type.FullName,
					type.Assembly.GetName().Name
					)
				)
			{

			}
		}
	}
}
