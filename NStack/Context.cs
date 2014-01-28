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
			new Context (act);
		}


		//Mutex for creations and destructions of context
		private static object mutex = new object ();

		//Reference between stack context names an their instances
		private static Dictionary<string, Context> contexts = new Dictionary<string,Context>();

		//Reference to previous created context on stack
		private Context previous = null;

		//Reference to registred services implementations
		private Dictionary<Type, object> instances = new Dictionary<Type, object>();

		//Find the last stacked context
		private static Context findCurrentContext ()
		{
			var id = new StackTrace ()
				.GetFrames ()
				.Where (x => x.GetMethod ().Name.StartsWith ("NContext+"))
				.Select (x => x.GetMethod ().Name).FirstOrDefault();

			return id == null ? null : contexts [id];
		}

		//Get the current implementation for the specified service
		public static T Resolve<T>()
		{
			//Find the las create context in thread
			var context = findCurrentContext ();

			//Fail in has no context
			if (context == null)
			{
				throw new OutOfContextException (typeof(T));
			}

			try 
			{
				//Try resolve service from context line
				return context.resolve<T> ();
			}
			catch
			{
				//or fail
				throw new IrresolvableServiceException (typeof(T));
			}
		}

		//Instance implementation for resolve
		private T resolve<T>()
		{
			lock(mutex)
			{
				try
				{
					//Try get service from this context
					return (T)this.instances[typeof(T)];
				}
				catch
				{
					//Or get from previous context
					return this.previous.resolve<T> ();
				}
			}
		}

		//Preserve the current conext of action for threding
		public static Action Save(Action action)
		{
			//Finc the current context
			var context = findCurrentContext ();

			//Fail in has no context
			if (context == null)
			{
				throw new OutOfContextException (null);
			}

			//Return an action that previously create preserved context
			return () => new Context(action, context);
		}

		public static void Register<T>(T instance)
		{
			//Lock another context modifications
			lock(mutex)
			{
				//Find the las create context in thread
				var context = findCurrentContext ();

				//Fail in has no context
				if (context == null)
				{
					throw new OutOfContextException (typeof(T));
				}

				try 
				{
					//Register current instance of service
					context.instances.Add (typeof(T), instance);
				}
				catch(Exception)
				{
					//Fail if is already registred
					throw new DuplicatedServiceRegistrationException (typeof(T));
				}
			}
		}

		//Initialization from a context
		private Context(Action act, Context previous = null)
		{
			//Method on stack to locate the current context
			DynamicMethod dynamicMethod = null;

			//Context creation
			lock(mutex)
			{
				this.previous = previous == null ? findCurrentContext () : previous;

				//Create a random method name to identify the context.
				dynamicMethod = new DynamicMethod (
					string.Format ("NContext+{0}", Guid.NewGuid ().ToString ("N")),
					null,
					new Type[] { typeof(Action) }
				);

				//Makes a body that calls the context body
				var body = dynamicMethod.GetILGenerator ();
				body.Emit (OpCodes.Ldarg_0);
				body.Emit (OpCodes.Callvirt, typeof(Action).GetMethod ("Invoke"));
				body.Emit (OpCodes.Ret);

				//Register the method id with the current context instance
				contexts.Add (dynamicMethod.Name, this);
			}

			try {

				//Execute the context body
				dynamicMethod.Invoke (null, new object[] { 
					act
				});

			} finally {

				//Context destruction
				lock (mutex)
				{
					contexts.Remove (dynamicMethod.Name);
				}
			}
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
