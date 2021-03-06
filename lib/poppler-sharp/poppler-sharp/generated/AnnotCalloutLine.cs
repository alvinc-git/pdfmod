// This file was generated by the Gtk# code generator.
// Any changes made will be lost if regenerated.

namespace Poppler {

	using System;
	using System.Collections;
	using System.Runtime.InteropServices;

#region Autogenerated code
	[StructLayout(LayoutKind.Sequential)]
	public struct AnnotCalloutLine {

		public bool Multiline;
		public double X1;
		public double Y1;
		public double X2;
		public double Y2;
		public double X3;
		public double Y3;

		public static Poppler.AnnotCalloutLine Zero = new Poppler.AnnotCalloutLine ();

		public static Poppler.AnnotCalloutLine New(IntPtr raw) {
			if (raw == IntPtr.Zero)
				return Poppler.AnnotCalloutLine.Zero;
			return (Poppler.AnnotCalloutLine) Marshal.PtrToStructure (raw, typeof (Poppler.AnnotCalloutLine));
		}

		[DllImport("poppler-glib")]
		static extern IntPtr poppler_annot_callout_line_new();

		public static AnnotCalloutLine New()
		{
			AnnotCalloutLine result = AnnotCalloutLine.New (poppler_annot_callout_line_new());
			return result;
		}

		[DllImport("poppler-glib")]
		static extern IntPtr poppler_annot_callout_line_copy(IntPtr raw);

		public Poppler.AnnotCalloutLine Copy() {
			IntPtr this_as_native = System.Runtime.InteropServices.Marshal.AllocHGlobal (System.Runtime.InteropServices.Marshal.SizeOf (this));
			System.Runtime.InteropServices.Marshal.StructureToPtr (this, this_as_native, false);
			IntPtr raw_ret = poppler_annot_callout_line_copy(this_as_native);
			Poppler.AnnotCalloutLine ret = Poppler.AnnotCalloutLine.New (raw_ret);
			ReadNative (this_as_native, ref this);
			System.Runtime.InteropServices.Marshal.FreeHGlobal (this_as_native);
			return ret;
		}

		[DllImport("poppler-glib")]
		static extern void poppler_annot_callout_line_free(IntPtr raw);

		public void Free() {
			IntPtr this_as_native = System.Runtime.InteropServices.Marshal.AllocHGlobal (System.Runtime.InteropServices.Marshal.SizeOf (this));
			System.Runtime.InteropServices.Marshal.StructureToPtr (this, this_as_native, false);
			poppler_annot_callout_line_free(this_as_native);
			ReadNative (this_as_native, ref this);
			System.Runtime.InteropServices.Marshal.FreeHGlobal (this_as_native);
		}

		[DllImport("poppler-glib")]
		static extern IntPtr poppler_annot_callout_line_get_type();

		public static GLib.GType GType { 
			get {
				IntPtr raw_ret = poppler_annot_callout_line_get_type();
				GLib.GType ret = new GLib.GType(raw_ret);
				return ret;
			}
		}

		static void ReadNative (IntPtr native, ref Poppler.AnnotCalloutLine target)
		{
			target = New (native);
		}

#endregion
	}
}
