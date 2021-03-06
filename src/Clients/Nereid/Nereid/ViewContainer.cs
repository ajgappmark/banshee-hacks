//
// ViewContainer.cs
//
// Author:
//   Aaron Bockover <abockover@novell.com>
//
// Copyright (C) 2007 Novell, Inc.
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using Gtk;
using Mono.Unix;

using Banshee.Widgets;
using Banshee.Gui.Widgets;
using Banshee.Sources.Gui;
using Banshee.Collection;

using Banshee.Gui;
using Banshee.ServiceStack;

namespace Nereid
{
    public class ViewContainer : VBox
    {
        private SearchEntry search_entry;
        private HBox header;
        private Alignment header_align;
        private EventBox header_box;

        private EventBox default_title_box;
        private Label title_label;
        private HBox custom_title_box;
        private Banshee.ContextPane.ContextPane context_pane;
        private VBox footer;

        private ISourceContents content;

        public ViewContainer ()
        {
            BuildHeader ();

            Spacing = 6;
            SearchSensitive = false;
        }

        private void BuildHeader ()
        {
            header_align = new Alignment (0.0f, 0.5f, 1.0f, 1.0f);
            if (Hyena.PlatformDetection.IsMeeGo) {
                header_align.RightPadding = 5;
                header_align.TopPadding = 5;
            }

            header = new HBox ();
            footer = new VBox ();

            default_title_box = new EventBox ();
            title_label = new Label ();
            title_label.Xalign = 0.0f;
            title_label.Ellipsize = Pango.EllipsizeMode.End;

            default_title_box.Add (title_label);

            // Show the source context menu when the title is right clicked
            default_title_box.PopupMenu += delegate {
                ServiceManager.Get<InterfaceActionService> ().SourceActions ["SourceContextMenuAction"].Activate ();
            };

            default_title_box.ButtonPressEvent += delegate (object o, ButtonPressEventArgs press) {
                if (press.Event.Button == 3) {
                    ServiceManager.Get<InterfaceActionService> ().SourceActions ["SourceContextMenuAction"].Activate ();
                }
            };

            header_box = new EventBox ();

            custom_title_box = new HBox () { Visible = false };

            BuildSearchEntry ();

            header.PackStart (default_title_box, true, true, 0);
            header.PackStart (custom_title_box, true, true, 0);
            header.PackStart (header_box, false, false, 0);
            header.PackStart (search_entry, false, false, 0);

            InterfaceActionService uia = ServiceManager.Get<InterfaceActionService> ();
            if (uia != null) {
                Gtk.Action action = uia.GlobalActions["WikiSearchHelpAction"];
                if (action != null) {
                    MenuItem item = new SeparatorMenuItem ();
                    item.Show ();
                    search_entry.Menu.Append (item);

                    item = new ImageMenuItem (Stock.Help, null);
                    item.Activated += delegate { action.Activate (); };
                    item.Show ();
                    search_entry.Menu.Append (item);
                }
            }

            header_align.Add (header);
            header_align.ShowAll ();
            search_entry.Show ();

            PackStart (header_align, false, false, 0);
            PackEnd (footer, false, false, 0);

            context_pane = new Banshee.ContextPane.ContextPane ();
            context_pane.ExpandHandler = b => {
                SetChildPacking (content.Widget, !b, true, 0, PackType.Start);
                SetChildPacking (context_pane, b, b, 0, PackType.End);
            };
            PackEnd (context_pane, false, false, 0);

            PackEnd (new ConnectedMessageBar (), false, true, 0);
        }

        private struct SearchFilter
        {
            public int Id;
            public string Field;
            public string Title;
        }

        private Dictionary<int, SearchFilter> search_filters = new Dictionary<int, SearchFilter> ();

        private void AddSearchFilter (TrackFilterType id, string field, string title)
        {
            SearchFilter filter = new SearchFilter ();
            filter.Id = (int)id;
            filter.Field = field;
            filter.Title = title;
            search_filters.Add (filter.Id, filter);
        }

        private void BuildSearchEntry ()
        {
            AddSearchFilter (TrackFilterType.None, String.Empty, Catalog.GetString ("Artist, Album, or Title"));
            AddSearchFilter (TrackFilterType.SongName, "title", Catalog.GetString ("Track Title"));
            AddSearchFilter (TrackFilterType.ArtistName, "artist", Catalog.GetString ("Artist Name"));
            AddSearchFilter (TrackFilterType.AlbumArtist, "albumartist", Catalog.GetString ("Album Artist"));
            AddSearchFilter (TrackFilterType.AlbumTitle, "album", Catalog.GetString ("Album Title"));
            AddSearchFilter (TrackFilterType.Genre, "genre", Catalog.GetString ("Genre"));
            AddSearchFilter (TrackFilterType.Year, "year", Catalog.GetString ("Year"));
            AddSearchFilter (TrackFilterType.Comment, "comment", Catalog.GetString ("Comment"));

            search_entry = new SearchEntry ();
            search_entry.SetSizeRequest (260, -1);

            foreach (SearchFilter filter in search_filters.Values) {
                search_entry.AddFilterOption (filter.Id, filter.Title);
                if (filter.Id == (int)TrackFilterType.None) {
                    search_entry.AddFilterSeparator ();
                }
            }

            search_entry.FilterChanged += OnSearchEntryFilterChanged;
            search_entry.ActivateFilter ((int)TrackFilterType.None);

            OnSearchEntryFilterChanged (search_entry, EventArgs.Empty);
        }

        private void OnSearchEntryFilterChanged (object o, EventArgs args)
        {
            /*search_entry.EmptyMessage = String.Format (Catalog.GetString ("Filter on {0}"),
                search_entry.GetLabelForFilterID (search_entry.ActiveFilterID));*/

            string query = search_filters.ContainsKey (search_entry.ActiveFilterID)
                ? search_filters[search_entry.ActiveFilterID].Field
                : String.Empty;

            search_entry.Query = String.IsNullOrEmpty (query) ? String.Empty : query + ":";

            Editable editable = search_entry.InnerEntry as Editable;
            if (editable != null) {
                editable.Position = search_entry.Query.Length;
            }
        }

        public void SetHeaderWidget (Widget widget)
        {
            if (widget != null) {
                header_box.Add (widget);
                widget.Show ();
                header_box.Show ();
            }
        }

        public void ClearHeaderWidget ()
        {
            header_box.Hide ();
            if (header_box.Child != null) {
                header_box.Remove (header_box.Child);
            }
        }

        public void SetFooter (Widget contents)
        {
            if (contents != null) {
                footer.PackStart (contents, false, false, 0);
                contents.Show ();
                footer.Show ();
            }
        }

        public void ClearFooter ()
        {
            footer.Hide ();
            foreach (Widget child in footer.Children) {
                footer.Remove (child);
            }
        }

        public Alignment Header {
            get { return header_align; }
        }

        public SearchEntry SearchEntry {
            get { return search_entry; }
        }

        public void SetTitleWidget (Widget widget)
        {
            var child = custom_title_box.Children.Length == 0 ? null : custom_title_box.Children[0];
            if (widget == child) {
                return;
            }

            if (child != null) {
                custom_title_box.Remove (child);
            }

            if (widget != null) {
                widget.HeightRequest = search_entry.Allocation.Height;
                widget.Show ();
                custom_title_box.PackStart (widget, false, false, 0);
            }

            custom_title_box.Visible = widget != null;
            default_title_box.Visible = widget == null;
        }

        public ISourceContents Content {
            get { return content; }
            set {
                if (content == value) {
                    return;
                }

                // Hide the old content widget
                if (content != null && content.Widget != null) {
                    content.Widget.Hide ();
                }

                // Add and show the new one
                if (value != null && value.Widget != null) {
                    PackStart (value.Widget, !context_pane.Large, true, 0);
                    value.Widget.Show ();
                }

                // Remove the old one
                if (content != null && content.Widget != null) {
                    Remove (content.Widget);
                }

                content = value;
            }
        }

        public string Title {
            set { title_label.Markup = String.Format ("<b>{0}</b>", GLib.Markup.EscapeText (value)); }
        }

        public bool SearchSensitive {
            get { return search_entry.Sensitive; }
            set {
                search_entry.Sensitive = value;
                search_entry.Visible = value;
            }
        }
    }
}
