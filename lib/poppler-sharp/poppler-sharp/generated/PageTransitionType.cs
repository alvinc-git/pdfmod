// This file was generated by the Gtk# code generator.
// Any changes made will be lost if regenerated.

namespace Poppler {

	using System;
	using System.Runtime.InteropServices;

#region Autogenerated code
	[GLib.GType (typeof (Poppler.PageTransitionTypeGType))]
	public enum PageTransitionType {

		Replace,
		Split,
		Blinds,
		Box,
		Wipe,
		Dissolve,
		Glitter,
		Fly,
		Push,
		Cover,
		Uncover,
		Fade,
	}

	internal class PageTransitionTypeGType {
		[DllImport ("poppler-glib")]
		static extern IntPtr poppler_page_transition_type_get_type ();

		public static GLib.GType GType {
			get {
				return new GLib.GType (poppler_page_transition_type_get_type ());
			}
		}
	}
#endregion
}