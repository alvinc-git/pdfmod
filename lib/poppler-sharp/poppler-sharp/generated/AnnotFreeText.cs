// This file was generated by the Gtk# code generator.
// Any changes made will be lost if regenerated.

namespace Poppler {

	using System;
	using System.Collections;
	using System.Runtime.InteropServices;

#region Autogenerated code
	public class AnnotFreeText : GLib.Object {

		[Obsolete]
		protected AnnotFreeText(GLib.GType gtype) : base(gtype) {}
		public AnnotFreeText(IntPtr raw) : base(raw) {}

		protected AnnotFreeText() : base(IntPtr.Zero)
		{
			CreateNativeObject (new string [0], new GLib.Value [0]);
		}

		[DllImport("poppler-glib")]
		static extern IntPtr poppler_annot_free_text_get_type();

		public static new GLib.GType GType { 
			get {
				IntPtr raw_ret = poppler_annot_free_text_get_type();
				GLib.GType ret = new GLib.GType(raw_ret);
				return ret;
			}
		}

		[DllImport("poppler-glib")]
		static extern IntPtr poppler_annot_free_text_get_callout_line(IntPtr raw);

		public Poppler.AnnotCalloutLine CalloutLine { 
			get {
				IntPtr raw_ret = poppler_annot_free_text_get_callout_line(Handle);
				Poppler.AnnotCalloutLine ret = Poppler.AnnotCalloutLine.New (raw_ret);
				return ret;
			}
		}

		[DllImport("poppler-glib")]
		static extern int poppler_annot_free_text_get_quadding(IntPtr raw);

		public Poppler.AnnotFreeTextQuadding Quadding { 
			get {
				int raw_ret = poppler_annot_free_text_get_quadding(Handle);
				Poppler.AnnotFreeTextQuadding ret = (Poppler.AnnotFreeTextQuadding) raw_ret;
				return ret;
			}
		}

#endregion
	}
}
