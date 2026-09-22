using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Permet aux générateurs de scène de renseigner des champs [SerializeField] privés.
    ///
    /// Pourquoi ne pas simplement rendre ces champs publics ? Parce qu'un champ public, c'est
    /// une invitation à être modifié par n'importe quel script à l'exécution. On garde donc
    /// l'encapsulation côté runtime, et c'est l'outil éditeur (et lui seul) qui câble, via
    /// SerializedObject — exactement comme si tu avais glissé la référence à la main dans l'Inspector.
    /// </summary>
    public static class SerializedWiring
    {
        public static void SetObject(Object target, string fieldName, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = Find(so, target, fieldName);
            if (property == null) return;

            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetFloat(Object target, string fieldName, float value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = Find(so, target, fieldName);
            if (property == null) return;

            property.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetString(Object target, string fieldName, string value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = Find(so, target, fieldName);
            if (property == null) return;

            property.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetBool(Object target, string fieldName, bool value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = Find(so, target, fieldName);
            if (property == null) return;

            property.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetInt(Object target, string fieldName, int value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = Find(so, target, fieldName);
            if (property == null) return;

            property.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetVector3(Object target, string fieldName, Vector3 value)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = Find(so, target, fieldName);
            if (property == null) return;

            property.vector3Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetEnum(Object target, string fieldName, int enumIndex)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = Find(so, target, fieldName);
            if (property == null) return;

            property.enumValueIndex = enumIndex;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Vérifie qu'une référence a bien été posée, et crie sinon.
        ///
        /// Un câblage raté est muet : le champ reste vide et le problème n'apparaît qu'au
        /// lancement du jeu, sous la forme d'un système qui ne fait rien. Le détecter au moment
        /// de la construction évite de chercher ailleurs.
        /// </summary>
        public static bool Verify(Object target, string fieldName)
        {
            SerializedObject so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(fieldName);

            if (property != null && property.propertyType == SerializedPropertyType.ObjectReference &&
                property.objectReferenceValue != null)
            {
                return true;
            }

            Debug.LogError("[UberBagarre] Cablage echoue : " + target.GetType().Name + "." + fieldName +
                           " est reste vide apres construction.", target);
            return false;
        }

        /// <summary>Ouvre un SerializedObject pour les cas complexes (listes, structures imbriquées).</summary>
        public static SerializedObject Open(Object target)
        {
            return new SerializedObject(target);
        }

        private static SerializedProperty Find(SerializedObject so, Object target, string fieldName)
        {
            SerializedProperty property = so.FindProperty(fieldName);
            if (property == null)
            {
                Debug.LogWarning("[UberBagarre] Cablage impossible : le champ '" + fieldName + "' est introuvable sur " +
                                 target.GetType().Name + ". Le nom du champ a-t-il change ?", target);
            }

            return property;
        }
    }
}
