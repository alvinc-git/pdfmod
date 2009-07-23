
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using Mono.Posix;
using Gtk;

using Hyena;
using Hyena.Gui;

using PdfSharp.Pdf;

using PdfMod.Actions;

namespace PdfMod
{
    public class GlobalActions : HyenaActionGroup
    {
        private PdfMod app;
        private UndoManager undo_manager;

        private static string [] require_doc_actions = new string[] {
            "SaveAction", "SaveAsAction", "UndoAction", "RedoAction", "SelectAllAction", "SelectEvensAction", "SelectOddsAction", "SelectMatchingAction"
        };

        private static string [] require_page_actions = new string[] {
            "RemoveAction", "ExtractAction", "ExportImagesAction", "RotateRightAction", "RotateLeftAction"
        };

        public GlobalActions (PdfMod app, ActionManager action_manager) : base (action_manager, "Global")
        {
            this.app = app;
            undo_manager = new UndoManager ();

            AddImportant (
                new ActionEntry ("OpenAction",   Gtk.Stock.Open,   null, "<control>O", Catalog.GetString ("Open a document"), OnOpen),
                new ActionEntry ("SaveAction",   Gtk.Stock.Save,   null, "<control>S", Catalog.GetString ("Save changes to this document, overwriting the existing file"), OnSave),
                new ActionEntry ("SaveAsAction", Gtk.Stock.SaveAs, null, "<control><shift>S", Catalog.GetString ("Save this document to a new file"), OnSaveAs),

                new ActionEntry ("FileMenuAction", null, Catalog.GetString ("_File"), null, null, null),
                new ActionEntry ("CloseAction", Gtk.Stock.Close, null, "<control>W", null, OnClose),
                new ActionEntry ("RemoveAction", Gtk.Stock.Remove, null, "Delete", null, OnRemove),
                new ActionEntry ("ExtractAction", null, "Extract Pages", null, null, OnExtractPages),
                new ActionEntry ("RotateRightAction", null, Catalog.GetString ("Rotate Right"), null, null, OnRotateRight),
                new ActionEntry ("RotateLeftAction", null, Catalog.GetString ("Rotate Left"), null, null, OnRotateLeft),
                new ActionEntry ("ExportImagesAction", null, Catalog.GetString ("Export Images..."), null, null, OnExportImages),

                new ActionEntry ("EditMenuAction", null, Catalog.GetString ("_Edit"), null, null, null),
                new ActionEntry ("SelectAllAction", Stock.SelectAll, null, "<control>A", null, OnSelectAll),
                new ActionEntry ("SelectEvensAction", null, Catalog.GetString ("Select Even Pages"), null, null, OnSelectEvens),
                new ActionEntry ("SelectOddsAction", null, Catalog.GetString ("Select Odd Pages"), null, null, OnSelectOdds),
                new ActionEntry ("SelectMatchingAction", null, Catalog.GetString ("Select Matching..."), "<control>F", null, OnSelectMatching),
                new ActionEntry ("UndoAction", Stock.Undo, null, "<control>z", null, OnUndo),
                new ActionEntry ("RedoAction", Stock.Redo, null, "<control>y", null, OnRedo),

                new ActionEntry ("ViewMenuAction", null, Catalog.GetString ("_View"), null, null, null),

                new ActionEntry ("PageContextMenuAction", null, "", null, null, OnPageContextMenu)
            );

            Update ();
            app.IconView.SelectionChanged += OnChanged;
            app.DocumentLoaded += OnChanged;
            undo_manager.UndoChanged += OnChanged;

            AddUiFromFile ("UIManager.xml");
            Register ();
        }

        private void OnChanged (object o, EventArgs args)
        {
            Update ();
        }

        private void Update ()
        {
            bool have_doc = app.Document != null;
            foreach (string action in require_doc_actions)
                UpdateAction (action, true, have_doc);

            bool have_page = have_doc && app.IconView.SelectedItems.Length > 0;
            foreach (string action in require_page_actions)
                UpdateAction (action, true, have_page);

            UpdateAction ("UndoAction", true, have_doc && undo_manager.CanUndo);
            UpdateAction ("RedoAction", true, have_doc && undo_manager.CanRedo);
            UpdateActions (true, have_doc && app.Document.HasUnsavedChanged, "SaveAction", "SaveAsAction");

            int selection_count = app.IconView.SelectedItems.Length;
            this["RemoveAction"].Label = String.Format (Catalog.GetPluralString (
                "Remove Page", "Remove {0} Pages", selection_count),
                selection_count);
            this["RemoveAction"].Tooltip = String.Format (Catalog.GetPluralString (
                "Remove the selected page", "Remove the {0} selected pages", selection_count),
                selection_count);
        }

#region Action Handlers

        private void OnOpen (object o, EventArgs args)
        {
            var chooser = new Gtk.FileChooserDialog (Catalog.GetString ("Select PDF"), app.Window, FileChooserAction.Open);
            chooser.AddFilter (GtkUtilities.GetFileFilter ("PDF Documents", new string [] {"pdf"}));
            chooser.AddFilter (GtkUtilities.GetFileFilter (Catalog.GetString ("All Files"), new string [] {"*"}));
            chooser.SelectMultiple = false;
            chooser.AddButton (Stock.Cancel, ResponseType.Cancel);
            chooser.AddButton (Stock.Open, ResponseType.Ok);
            chooser.DefaultResponse = ResponseType.Ok;

            if (chooser.Run () == (int)ResponseType.Ok) {
                string filename = chooser.Filename;
                Log.DebugFormat ("Opening {0}", filename);
                var new_app = app.Document == null ? app : new PdfMod ();
                PdfMod.RunIdle (delegate { new_app.LoadPath (filename); });
            }
            
            chooser.Destroy ();
        }

        private void OnSave (object o, EventArgs args)
        {
            app.Document.Save (app.Document.SuggestedSavePath);
            undo_manager.Clear ();
        }

        private void OnSaveAs (object o, EventArgs args)
        {
            var chooser = new Gtk.FileChooserDialog (Catalog.GetString ("Save as..."), app.Window, FileChooserAction.Save);
            chooser.SelectMultiple = false;
            chooser.AddButton (Stock.Cancel, ResponseType.Cancel);
            chooser.AddButton (Stock.SaveAs, ResponseType.Ok);
            chooser.AddFilter (GtkUtilities.GetFileFilter ("PDF Documents", new string [] {"pdf"}));
            chooser.AddFilter (GtkUtilities.GetFileFilter (Catalog.GetString ("All Files"), new string [] {"*"}));
            chooser.SetCurrentFolder (System.IO.Path.GetDirectoryName (app.Document.SuggestedSavePath));
            chooser.CurrentName = System.IO.Path.GetFileName (app.Document.SuggestedSavePath);
            chooser.DefaultResponse = ResponseType.Ok;

            var response = chooser.Run ();
            string filename = chooser.Filename;
            chooser.Destroy ();
            
            if (response == (int)ResponseType.Ok) {
                Log.DebugFormat ("Saving {0} to {1}", app.Document.Uri, filename);
                app.Document.Save (filename);
                undo_manager.Clear ();
            }
        }

        private void OnRemove (object o, EventArgs args)
        {
            var action = new RemoveAction (app.Document, app.IconView.SelectedPages);
            action.Do ();
            undo_manager.AddUndoAction (action);

            /*var paths = new List<TreePath> (app.IconView.SelectedItems);
            foreach (var path in paths) {
                TreeIter iter;
                app.IconView.Store.GetIter (out iter, path);
                app.IconView.Store.Remove (ref iter);
            }

            app.IconView.Store.Refresh ();*/
        }

        private void OnExtractPages (object o, EventArgs args)
        {
            var doc = new PdfDocument ();
            var pages = new List<Page> (app.IconView.SelectedPages);
            pages.Sort ((a, b) => { return a.Index < b.Index ? -1 : 1; });
            foreach (var page in pages) {
                Console.WriteLine ("have page {0}", page.Index);
                doc.AddPage (page.Pdf);
            }

            var path = PdfMod.GetTmpFilename ();
            doc.Save (path);
            doc.Dispose ();

            var new_app = new PdfMod ();
            new_app.LoadPath (path, Path.Combine (
                Path.GetDirectoryName (app.Document.SuggestedSavePath),
                String.Format ("[{0}] {1}",
                    GLib.Markup.EscapeText (GetPageSummary (pages)),
                    Path.GetFileName (app.Document.SuggestedSavePath))
            ));
        }

        // Return a simple, nice string describing the selected pages
        //   e.g.  Page 1, or Page 3 - 6, or Page 2, 4, 6
        private string GetPageSummary (List<Page> pages)
        {
            string pages_summary = null;
            if (pages.Count == 1) {
                // Translators: {0} is the number of pages (always 1), and {1} is the page number, eg Page 1, or Page 5
                pages_summary = String.Format (Catalog.GetPluralString ("Page {1}", "Page {1}", pages.Count), pages.Count, pages[0].Index + 1);
            } else if (pages[0].Index + pages.Count - 1 == pages[pages.Count - 1].Index) {
                // Translators: {0} is the number of pages, and {1} is the first page, {2} is the last page,
                // eg Pages 3 - 7
                pages_summary = String.Format (Catalog.GetPluralString ("Pages {1} - {2}", "Pages {1} - {2}", pages.Count),
                    pages.Count, pages[0].Index + 1, pages[pages.Count - 1].Index + 1);
            } else if (pages.Count < 10) {
                string page_nums = String.Join (", ", pages.Select (p => (p.Index + 1).ToString ()).ToArray ());
                // Translators: {0} is the number of pages, {1} is a comma separated list of page numbers, eg Pages 1, 4, 9
                pages_summary = String.Format (Catalog.GetPluralString ("Pages {1}", "Pages {1}", pages.Count), pages.Count, page_nums);
            } else {
                // Translators: {0} is the number of pages, eg 12 Pages
                pages_summary = String.Format (Catalog.GetPluralString ("{0} Page", "{0} Pages}", pages.Count), pages.Count);
            }
            return pages_summary;
        }

        private void OnExportImages (object o, EventArgs args)
        {
            var pages = app.IconView.SelectedPages.ToList ();
            var action = new ExportImagesAction (app.Document, pages);
            if (action.ExportableImageCount == 0) {
                Log.Information ("Found zero exportable images in the selected pages");
                return;
            }

            var export_path_base = Path.Combine (
                Path.GetDirectoryName (app.Document.SuggestedSavePath),
                // Translators: This is used for creating a folder name, be careful!
                String.Format (Catalog.GetString ("{0} - Images for {1}"), app.Document.TitleOrFilename, GetPageSummary (pages))
            );

            var export_path = export_path_base;
            int i = 1;
            while (Directory.Exists (export_path)) {
                export_path = String.Format ("{0} ({1})", export_path_base, i++);
            }

            Directory.CreateDirectory (export_path);

            action.Do (export_path);
            System.Diagnostics.Process.Start (export_path);
        }

        private void OnUndo (object o, EventArgs args)
        {
            undo_manager.Undo ();
        }

        private void OnRedo (object o, EventArgs args)
        {
            undo_manager.Redo ();
        }

        private void OnPageContextMenu (object o, EventArgs args)
        {
            ShowContextMenu ("/PageContextMenu");
        }

        private void OnSelectAll (object o, EventArgs args)
        {
            app.IconView.SetPageSelectionMode (PageSelectionMode.All);
        }

        private void OnSelectEvens (object o, EventArgs args)
        {
            app.IconView.SetPageSelectionMode (PageSelectionMode.Evens);
        }

        private void OnSelectOdds (object o, EventArgs args)
        {
            app.IconView.SetPageSelectionMode (PageSelectionMode.Odds);
        }

        private void OnSelectMatching (object o, EventArgs args)
        {
            app.ToggleMatchQuery ();
        }

        private void OnRotateRight (object o, EventArgs args)
        {
            var action = new RotateAction (app.Document, app.IconView.SelectedPages, 90);
            action.Do ();
            undo_manager.AddUndoAction (action);
        }

        private void OnRotateLeft (object o, EventArgs args)
        {
            var action = new RotateAction (app.Document, app.IconView.SelectedPages, -90);
            action.Do ();
            undo_manager.AddUndoAction (action);
        }

        private void OnClose (object o, EventArgs args)
        {
            app.Quit ();
        }

#endregion

    }
}
