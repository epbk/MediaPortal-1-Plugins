using System;
using System.Collections.Generic;
using System.Text;
using MediaPortal.Pbk.Cornerstone.Database;
using System.Windows.Forms;
using MediaPortal.Pbk.Cornerstone.Database.Tables;

namespace MediaPortal.Pbk.Cornerstone.GUI.Controls {
    public interface IDBFieldBackedControl: IDBBackedControl {
        event FieldChangedListener FieldChanged;

        String DatabaseFieldName {
            get;
            set;
        }

        DBField DatabaseField {
            get;
        }

        DBField.DBDataType DBTypeOverride {
            get;
            set;
        } 
    }

    public delegate void FieldChangedListener(DatabaseTable obj, DBField field, object value);
}
