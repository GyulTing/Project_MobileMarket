using System.Collections.Generic;
using UnityEngine;

namespace MobileMarket.EditorTools
{
    /// <summary>
    /// Hand edits made to the generated Excavation site, kept so a rebuild doesn't lose them: objects moved, rotated or
    /// rescaled from where the builder put them, and objects deleted. Keys are hierarchy paths under the site root with
    /// an occurrence index for same-named siblings (e.g. "Laboratory/P_EX_Lab_LampPost#3"). Objects added by hand live
    /// under the separate ExcavationSite_Custom root instead, which the builder never touches.
    /// </summary>
    public class ExcavationSiteEdits : ScriptableObject
    {
        [System.Serializable]
        public class Placement
        {
            public string key;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
        }

        public List<Placement> moved = new List<Placement>();
        public List<string> removed = new List<string>();
    }
}
