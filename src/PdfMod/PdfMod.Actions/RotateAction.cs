
using System;
using System.Collections.Generic;

using Mono.Unix;

namespace PdfMod.Actions
{
    public class RotateAction : BasePageAction
    {
        private int rotation;

        public RotateAction (Document document, IEnumerable<Page> pages, int rotation) : base (document, pages)
        {
            this.rotation = rotation;
            Description = String.Format (Catalog.GetPluralString ("Rotate {1}", "Rotate {1}", Pages.Count),
                Pages.Count, GlobalActions.GetPageSummary (Pages, 5));
        }

        public override void Undo ()
        {
            Document.Rotate (Pages.ToArray (), -rotation);
        }

        public override void Redo ()
        {
            Document.Rotate (Pages.ToArray (), rotation);
        }
    }
}
