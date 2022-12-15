using System;
using System.Collections.Generic;
using Unity.Properties;
using Unity.RuntimeSceneSerialization.Prefabs;
using Unity.XRTools.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace Unity.AR.Companion.Core
{
    interface IProxyListViewPropertyData
    {
        string DisplayName { get; }
        string PropertyName { get; }
        void Setup(Toggle optionalConstraintToggle, object typelessContainer);
        void OnOptionalConstraintToggleValueChanged(bool value);
        Type DeclaredValueType();
        object GetValue();
        void SetValue<TValue>(TValue value);
        bool HasAttribute<T>() where T : Attribute;
    }

    class ProxyListViewPropertyData<TContainer> : ProxyListViewData, IProxyListViewPropertyData
    {
        readonly string m_DisplayName;
        readonly string m_PropertyPath;
        TContainer m_TypedContainer;
        readonly IProperty<TContainer> m_Property;

        IProperty<TContainer> Property => m_Property;
        IProperty<TContainer> OptionalConstraintProperty { get; set; }
        public string DisplayName => m_DisplayName;
        public string PropertyName => Property.Name;

        public ProxyListViewPropertyData(string template, TContainer container, int index, int depth, List<ProxyListViewData> children,
            IProperty<TContainer> property, string propertyPath, PrefabMetadata prefabMetadata, string transformPath, int componentIndex)
            : base(template, container, index, depth, prefabMetadata, transformPath, componentIndex, children)
        {
            m_PropertyPath = propertyPath;
            m_Property = property;
            m_DisplayName = ReflectionUtils.NicifyVariableName(property.Name);
            m_TypedContainer = container;
        }

        public void SetOptionalConstraintProperty(IProperty property)
        {
            if (property is IProperty<TContainer> typedProperty)
                OptionalConstraintProperty = typedProperty;
            else
                Debug.LogError($"Could not convert {property} to {typeof(IProperty<TContainer>)}");
        }

        public void Setup(Toggle optionalConstraintToggle, object typelessContainer)
        {
            var typedContainer = (TContainer)typelessContainer;
            var hasOptionalConstraint = OptionalConstraintProperty != null;
            optionalConstraintToggle.gameObject.SetActive(hasOptionalConstraint);
            if (hasOptionalConstraint)
                optionalConstraintToggle.SetIsOnWithoutNotify((bool)OptionalConstraintProperty.GetValue(ref typedContainer));
        }

        public void OnOptionalConstraintToggleValueChanged(bool value)
        {
            OptionalConstraintProperty.SetValue(ref m_TypedContainer, value);
        }

        public Type DeclaredValueType() { return Property.DeclaredValueType(); }

        public object GetValue()
        {
            return Property.GetValue(ref m_TypedContainer);
        }

        public void SetValue<TValue>(TValue value)
        {
            if (m_PrefabMetadata)
                m_PrefabMetadata.SetPropertyOverride(m_PropertyPath, m_TransformPath, m_ComponentIndex, value);

            Property.SetValue(ref m_TypedContainer, value);
        }

        public bool HasAttribute<T>() where T : Attribute
        {
            return Property.HasAttribute<T>();
        }
    }
}
