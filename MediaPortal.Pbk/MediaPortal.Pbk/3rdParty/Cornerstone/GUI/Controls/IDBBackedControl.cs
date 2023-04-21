using System;
using System.Collections.Generic;
using System.Text;
using MediaPortal.Pbk.Cornerstone.Database.Tables;
using System.ComponentModel;

namespace MediaPortal.Pbk.Cornerstone.GUI.Controls {
    public interface IDBBackedControl {
        // The database object type that the control displays data about.
        Type Table {
            get;
            set;
        }

        // The object cotnaining the data to be displayed.
        DatabaseTable DatabaseObject {
            get;
            set;
        }
    }
}
