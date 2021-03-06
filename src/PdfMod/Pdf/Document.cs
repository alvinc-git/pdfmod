// Copyright (C) 2009-2010 Novell, Inc.
// Copyright (C) 2009 Julien Rebetez
// Copyright (C) 2009 Michael McKinley
//
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

using System;
using System.Linq;
using System.Collections.Generic;

using Mono.Unix;

using Hyena;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace PdfMod.Pdf
{
    public class Document : IDisposable
    {
        PdfDocument pdf_document;
        List<Page> pages = new List<Page> ();
        string tmp_path;
        string tmp_uri;
        PageLabels page_labels;
        Poppler.Document poppler_doc;
        uint save_timeout_id = 0;

        internal string CurrentStateUri { get { return tmp_uri ?? Uri; } }

        public PageLabels Labels { get { return page_labels; } }
        public string SuggestedSavePath { get; set; }
        public string Uri { get; private set; }
        public string Path { get; private set; }
        public PdfDocument Pdf { get { return pdf_document; } }
        public int Count { get { return pages.Count; } }

        public IEnumerable<Page> Pages {
            get {
                foreach (var page in pages)
                    yield return page;
            }
        }

        public Page this [int index] {
            get { return pages[index]; }
        }

        public string Title {
            get {
                var title = Pdf.Info.Title;
                return String.IsNullOrEmpty (title) ? null : title;
            }
            set {
                if (value.Trim () == "")
                    value = null;

                if (value == Title)
                    return;

                Pdf.Info.Title = value;
                StartSaveTempTimeout ();
            }
        }

        public string Author {
            get { return Pdf.Info.Author; }
            set {
                if (value.Trim () == "")
                    value = null;

                if (value == Author)
                    return;

                Pdf.Info.Author = value;
                StartSaveTempTimeout ();
            }
        }

        public string Keywords {
            get { return Pdf.Info.Keywords; }
            set {
                if (value.Trim () == "")
                    value = null;

                if (value == Keywords)
                    return;

                Pdf.Info.Keywords = value;
                StartSaveTempTimeout ();
            }
        }

        public string Subject {
            get { return Pdf.Info.Subject; }
            set {
                if (value.Trim () == "")
                    value = null;

                if (value == Subject)
                    return;

                Pdf.Info.Subject = value;
                StartSaveTempTimeout ();
            }
        }

        public string Filename {
            get { return System.IO.Path.GetFileName (SuggestedSavePath); }
        }

        public string Password { get; set; }

        public event System.Action Changed;
        public event System.Action PagesMoved;
        public event Action<Page []> PagesRemoved;
        public event Action<int, Page []> PagesAdded;
        public event Action<Page []> PagesChanged;

        public Document ()
        {
        }

        public void Load (string uri, PdfPasswordProvider passwordProvider, bool isAlreadyTmp)
        {
            if (isAlreadyTmp) {
                tmp_uri = new Uri (uri).AbsoluteUri;
                tmp_path = new Uri (uri).LocalPath;
            }

            var safe_uri = new Hyena.SafeUri (uri);
            var uri_obj = new Uri (safe_uri.AbsoluteUri);
            Uri = uri_obj.AbsoluteUri;
            SuggestedSavePath = Path = uri_obj.LocalPath;

            pdf_document = PdfSharp.Pdf.IO.PdfReader.Open (Path, PdfDocumentOpenMode.Modify, passwordProvider);

            for (int i = 0; i < pdf_document.PageCount; i++) {
                var page = new Page (pdf_document.Pages[i]) {
                    Document = this,
                    Index = i
                };
                pages.Add (page);
            }

            page_labels = new PageLabels (pdf_document);
            ExpireThumbnails (pages);
            OnChanged ();
        }

        public bool HasUnsavedChanges {
            get { return UnsavedChanges > 0 || tmp_uri != null || save_timeout_id != 0; }
        }

        int unsaved_changes = 0;
        public int UnsavedChanges {
            get { return unsaved_changes; }
            set {
                unsaved_changes = value;
                OnChanged ();
            }
        }

        public long FileSize {
            get { return new System.IO.FileInfo (tmp_path ?? Path).Length; }
        }

        public void Dispose ()
        {
            if (pdf_document != null) {
                pdf_document.Dispose ();
                pdf_document = null;
            }

            if (tmp_path != null) {
                System.IO.File.Delete (tmp_path);
                tmp_path = tmp_uri = null;
            }
        }

        public IEnumerable<Page> FindPagesMatching (string text)
        {
            using (var doc = Poppler.Document.NewFromFile (tmp_uri ?? Uri, Password ?? "")) {
                for (int i = 0; i < doc.NPages; i++) {
                    using (var page = doc.GetPage (i)) {
                        var list = page.FindText (text);
                        if (list != null && list.Count > 0) {
                            yield return pages[i];
                            list.Dispose ();
                        }
                    }
                }
            }
        }

        public void Move (int to_index, Page [] move_pages, int [] new_indexes)
        {
            // Remove all the pages
            foreach (var page in move_pages) {
                pages.Remove (page);
            }

            if (new_indexes == null) {
                new_indexes = move_pages.Select (p => to_index++).ToArray ();
            }

            // Add back at the right index
            for (int i = 0; i < move_pages.Length; i++) {
                pages.Insert (new_indexes[i], move_pages[i]);
            }

            // Do the actual move in the document
            foreach (var page in move_pages) {
                pdf_document.Pages.MovePage (page.Index, IndexOf (page));
            }

            Reindex ();

            SaveTemp ();

            var handler = PagesMoved;
            if (handler != null) {
                handler ();
            }
        }

        public int IndexOf (Page page)
        {
            return pages.IndexOf (page);
        }

        public void Remove (params Page [] remove_pages)
        {
            foreach (var page in remove_pages) {
                pdf_document.Pages.Remove (page.Pdf);
                pages.Remove (page);
            }

            Reindex ();
            SaveTemp ();

            var handler = PagesRemoved;
            if (handler != null) {
                handler (remove_pages);
            }
        }

        public void Rotate (Page [] rotate_pages, int rotate_by)
        {
            foreach (var page in rotate_pages) {
                page.Pdf.Rotate += rotate_by;
            }

            OnPagesChanged (rotate_pages);
        }

        public void Save (string uri)
        {
            Pdf.Save (uri);
            UnsavedChanges = 0;
            Log.DebugFormat ("Saved to {0}", uri);

            var uri_obj = new Uri (uri);
            Uri = uri;
            SuggestedSavePath = Path = uri_obj.LocalPath;

            if (tmp_uri != null) {
                try {
                    System.IO.File.Delete (tmp_path);
                } catch (Exception e) {
                    Log.Exception ("Couldn't delete tmp file after saving", e);
                } finally {
                    tmp_uri = tmp_path = null;
                }
            }

            OnChanged ();
        }

        public void AppendFromUri (Uri uri)
        {
            AddFromUri (uri, Count);
        }

        public void AddFromUri (Uri uri)
        {
            AddFromUri (uri, 0);
        }

        public void AddFromUri (Uri uri, int to_index)
        {
            AddFromUri (uri, to_index, null);
        }

        public void AddFromUri (Uri uri, int to_index, int [] pages_to_import)
        {
            Log.DebugFormat ("Inserting pages from {0} to index {1}", uri, to_index);
            using (var doc = PdfSharp.Pdf.IO.PdfReader.Open (uri.LocalPath, null, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import)) {
                var pages = new List<Page> ();
                for (int i = 0; i < doc.PageCount; i++) {
                    if (pages_to_import == null || pages_to_import.Contains (i)) {
                        pages.Add (new Page (doc.Pages [i]));
                    }
                }
                Add (to_index, pages.ToArray ());
                to_index += pages.Count;
            }
        }

        public void Add (int to_index, params Page [] add_pages)
        {
            int i = to_index;
            foreach (var page in add_pages) {
                var pdf = pdf_document.Pages.Insert (i, page.Pdf);
                page.Pdf = pdf;
                page.Document = this;
                pages.Insert (i, page);
                i++;
            }

            Reindex ();
            SaveTemp ();
            ExpireThumbnails (add_pages);

            var handler = PagesAdded;
            if (handler != null) {
                handler (to_index, add_pages);
            }
        }

        Poppler.Document PopplerDoc {
            get {
                if (poppler_doc == null) {
                    poppler_doc = Poppler.Document.NewFromFile (tmp_uri ?? Uri, Password ?? "");
                }
                return poppler_doc;
            }
        }

        void ExpireThumbnails (IEnumerable<Page> update_pages)
        {
            if (poppler_doc != null) {
                poppler_doc.Dispose ();
                poppler_doc = null;
            }

            foreach (var page in update_pages) {
                page.SurfaceDirty = true;
            }
        }

        public PageThumbnail GetSurface (Page page, int w, int h, int min_width)
        {
            if (w < min_width || h < min_width) {
                return null;
            }

            using (var ppage = PopplerDoc.GetPage (page.Index)) {
                double pw, ph;
                ppage.GetSize (out pw, out ph);
                double scale = Math.Min (w / pw, h / ph);

                var surface = new Cairo.ImageSurface (Cairo.Format.Argb32, (int)(scale * pw), (int)(scale * ph));
                var cr = new Cairo.Context (surface);
                cr.Scale (scale, scale);
                ppage.Render (cr);
                page.SurfaceDirty = false;
                return new PageThumbnail () { Surface = surface, Context = cr };
            }
        }

        void Reindex ()
        {
            for (int i = 0; i < pages.Count; i++) {
                pages[i].Index = i;
            }
        }

        void StartSaveTempTimeout ()
        {
            if (save_timeout_id != 0) {
                GLib.Source.Remove (save_timeout_id);
            }
            
            save_timeout_id = GLib.Timeout.Add (100, OnSaveTempTimeout);
        }

        bool OnSaveTempTimeout ()
        {
            save_timeout_id = 0;
            SaveTemp ();
            return false;
        }

        void SaveTemp ()
        {
            try {
                if (tmp_path == null) {
                    tmp_path = Core.Client.GetTmpFilename ();
                    if (System.IO.File.Exists (tmp_path)) {
                        System.IO.File.Delete (tmp_path);
                    }
                    tmp_uri = new Uri (tmp_path).AbsoluteUri;
                }

                pdf_document.Save (tmp_path);
                Log.DebugFormat ("Saved tmp file to {0}", tmp_path);
                OnChanged ();
            } catch (Exception e) {
                Log.Exception ("Failed to save tmp document", e);
                // TODO tell user, shutdown
            }
        }

        void OnPagesChanged (Page [] changed_pages)
        {
            Reindex ();
            SaveTemp ();
            ExpireThumbnails (changed_pages);

            var handler = PagesChanged;
            if (handler != null) {
                handler (changed_pages);
            }
            OnChanged ();
        }

        void OnChanged ()
        {
            var handler = Changed;
            if (handler != null) {
                handler ();
            }
        }

        // Return a simple, nice string describing the selected pages
        //   e.g.  Page 1, or Page 3 - 6, or Page 2, 4, 6
        public static string GetPageSummary (List<Page> pages, int maxListed)
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
            } else if (pages.Count < maxListed) {
                string page_nums = String.Join (", ", pages.Select (p => (p.Index + 1).ToString ()).ToArray ());
                // Translators: {0} is the number of pages, {1} is a comma separated list of page numbers, eg Pages 1, 4, 9
                pages_summary = String.Format (Catalog.GetPluralString ("Pages {1}", "Pages {1}", pages.Count), pages.Count, page_nums);
            } else {
                // Translators: {0} is the number of pages, eg 12 Pages
                pages_summary = String.Format (Catalog.GetPluralString ("{0} Page", "{0} Pages", pages.Count), pages.Count);
            }
            return pages_summary;
        }

    }
}
