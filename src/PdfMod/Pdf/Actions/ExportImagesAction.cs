//
// ExportImagesAction.cs
//
// Based on the PDFSharp example, "Based on GDI+/ExportImages/Program.cs"
//
// Authors:
//   PDFsharp Team (mailto:PDFsharpSupport@pdfsharp.de)
//
// Modified by:
//   Gabriel Burt <gabriel.burt@gmail.com>
//
// Copyright (c) 2005-2008 empira Software GmbH, Cologne (Germany)
// Copyright (C) 2009 Novell, Inc.
//
// http://www.pdfsharp.com
// http://sourceforge.net/projects/pdfsharp
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included
// in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
// THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;

namespace PdfMod.Pdf.Actions
{
    public class ExportImagesAction
    {
        List<ImageInfo> image_objects;

        public ExportImagesAction (Document document, IEnumerable<Page> pages)
        {
            image_objects = GetImageObjectsFrom (pages).Where (IsExportable).ToList ();
        }

        public int ExportableImageCount {
            get { return image_objects.Count; }
        }

        public void Do (string to_path)
        {
            foreach (var img_obj in image_objects) {
                Export (img_obj, to_path);
            }
        }

        IEnumerable<ImageInfo> GetImageObjectsFrom (IEnumerable<Page> pages)
        {
            // Doesn't seem like you can get the images just on one page; the following
            // gets all the images in the whole document, so only need to do it from one page
            //foreach (var page in pages) {
                var page = pages.First ();
                var resources = page.Pdf.Elements.GetDictionary ("/Resources");
                if (resources == null)
                    yield break;

                var x_objects = resources.Elements.GetDictionary ("/XObject");
                if (x_objects == null)
                    yield break;

                int i = 0;
                var items = x_objects.Elements.Values;
                foreach (var item in items) {
                    var reference = item as PdfReference;
                    if (reference == null)
                        continue;

                    var x_object = reference.Value as PdfDictionary;
                    // Put this in a variable to pass to GetString so that it's not pulled out as a translation string
                    var subtype = "/Subtype";
                    if (x_object != null && x_object.Elements.GetString (subtype) == "/Image") {
                        yield return new ImageInfo () { Page = page, ImageObject = x_object, PageIndex = i++ };
                    }
                }
            //}
        }

        bool IsExportable (ImageInfo image)
        {
            var filter = image.ImageObject.Elements.GetName("/Filter");
            return filter == "/DCTDecode" || filter == "/FlateDecode";
        }

        /// <summary>
        /// Currently extracts only JPEG images.
        /// </summary>
        static void Export (ImageInfo image, string to_path)
        {
            string filter = image.ImageObject.Elements.GetName("/Filter");
            switch (filter) {
                case "/DCTDecode":
                    ExportJpegImage (image, GetFilename (image, to_path, "jpg"));
                    break;
                case "/FlateDecode":
                    ExportAsPngImage (image, GetFilename (image, to_path, "png"));
                    break;
            }
        }

        static string GetFilename (ImageInfo image, string to_path, string ext)
        {
            var name = image.ImageObject.Elements.GetName ("/Name");
            var name_fragment = String.IsNullOrEmpty (name) ? null : String.Format (" ({0})", name);
            var path = Path.Combine (
                to_path,
                String.Format ("{0:00}{1}.{2}",
                    image.PageIndex, name_fragment, ext)
                //String.Format ("Page {0} - #{1:00}{2}.{3}",
                    //image.Page.Index, image.PageIndex, name_fragment, ext)
            );
            return path;
        }

        /// <summary>
        /// Exports a JPEG image.
        /// </summary>
        static void ExportJpegImage (ImageInfo image, string path)
        {
            // Fortunately JPEG has native support in PDF and exporting an image is just writing the stream to a file.
            var fs = new FileStream (path, FileMode.Create, FileAccess.Write);

            byte[] stream = image.ImageObject.Stream.Value;
            using (var bw = new BinaryWriter (fs)) {
                bw.Write (stream);
            }
        }

        /// <summary>
        /// Exports image in PNG format.
        /// </summary>
        static void ExportAsPngImage (ImageInfo image, string path)
        {
            int width = image.ImageObject.Elements.GetInteger (PdfImage.Keys.Width);
            int height = image.ImageObject.Elements.GetInteger (PdfImage.Keys.Height);

            try {
                byte [] data = image.ImageObject.Stream.UnfilteredValue;
                using (var pixbuf = new Gdk.Pixbuf (data, Gdk.Colorspace.Rgb, false, 8, width, height, width*3)) {
                    pixbuf.Save (path, "png");
                }
            } catch (Exception e) {
                Hyena.Log.Exception ("Unable to load PNG from embedded PDF object", e);
            }
        }

        class ImageInfo {
            public Page Page { get; set; }
            public PdfDictionary ImageObject { get; set; }
            public int PageIndex { get; set; }
        }
    }
}
