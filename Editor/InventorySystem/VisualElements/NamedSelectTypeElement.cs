using System;
using fefek5.Toys.Editor.VisualElements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace SteamToys.Editor.InventorySystem
{
    /// <summary>
    /// A <see cref="SelectTypeElement"/> named after its value, what <c>ToString</c> of the value returns, instead of the
    /// property. Without a value it keeps the name of the property.
    /// </summary>
    public class NamedSelectTypeElement : VisualElement
    {
        /// <param name="property">The [SerializeReference] property</param>
        /// <param name="fieldType">The type every type to pick from is assignable to</param>
        /// <param name="createContent">What the foldout shows for the current value; by default its fields</param>
        public NamedSelectTypeElement(SerializedProperty property, Type fieldType,
            Func<SerializedProperty, VisualElement> createContent = null)
        {
            var element = new SelectTypeElement(property, fieldType, createContent);
            var path = property.propertyPath;

            Add(element);
            Refresh(property);

            // Tracked here, which a pick does not bind again the way it does the element.
            // The value is a managed reference, whose changes the property does not report.
            this.TrackSerializedObjectValue(property.serializedObject, changed => Refresh(changed.FindProperty(path)));

            return;

            void Refresh(SerializedProperty changed)
            {
                if (changed != null)
                    element.Label = changed.managedReferenceValue?.ToString() ?? changed.displayName;
            }
        }
    }
}
