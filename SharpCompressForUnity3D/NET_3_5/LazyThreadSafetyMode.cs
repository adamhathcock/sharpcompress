#define NET_4_0
#if NET_4_0
	
	using System;
	
	namespace System.Threading
	{
		public enum LazyThreadSafetyMode
		{
			None,
			PublicationOnly,
			ExecutionAndPublication
		}
	}
		
	#endif