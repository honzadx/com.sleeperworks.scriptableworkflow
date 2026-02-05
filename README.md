# Scriptable Flow

Scriptable Flow is a Unity package that streamlines ScriptableObject creation by eliminating clutter from the Project window context menu and replacing it with a single, unified creation path.

### Features
1. <b>Single creation menu</b> - All ScriptableObjects available in `Create/Scriptable Object`
2. <b>Filtering</b> - Filter ScriptableObjects by type and customizable category
3. <b>Templates</b> - Use a reference asset and modify it for faster iteration
4. <b>Previews before creation</b> - View and edit data before it's serialized into an asset

### Basics
To make your ScriptableObjects discoverable, you must mark them with either Unity's `CreateAssetMenu` attribute or this package's `CreateAsset` attribute.

The `CreateAsset` attribute additionaly allows you to specify the asset's category and whether it applies to inherited classes. If no category is specified, it defaults to the "None" category.

```csharp
[CreateAsset(inherit: true, category: "AI")]
class abstract AIBehaviourTemplate : ScriptableObject {...}
```

After setting up your ScriptableObjects to be discoverable by the editor window, you can generate them by right-clicking in the Project window, selecting `Create/Scriptable Object`, choosing your type, and clicking the Create button.

### Preview
<p align="center-top">
    <img src="Preview~/2.png" alt="2" width="45%" style="vertical-align: top;"/>
    <img src="Preview~/1.png" alt="1" width="45%" style="vertical-align: top;"/>
</p>
