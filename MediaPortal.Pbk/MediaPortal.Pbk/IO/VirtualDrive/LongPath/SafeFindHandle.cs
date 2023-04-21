using Microsoft.Win32.SafeHandles;

namespace MediaPortal.Pbk.IO.VirtualDrive.LongPath
{
	internal sealed class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid
	{
		internal SafeFindHandle()
			: base(true)
		{
		}

		protected override bool ReleaseHandle()
		{
			return NativeMethods.FindClose(base.handle);
		}
	}
}