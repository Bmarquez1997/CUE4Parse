using CUE4Parse.UE4.Assets.Exports.CustomizableObject.Mutable;
using CUE4Parse.UE4.Assets.Readers;
using Newtonsoft.Json;

namespace CUE4Parse.UE4.Assets.Exports.CustomizableObject;

public class UCustomizableObject : UObject
{
    public long InternalVersion;
    public FModel? Model;
    public FPackageIndex Private;
    
    public override void Deserialize(FAssetArchive Ar, long validPos)
    {
        base.Deserialize(Ar, validPos);
        // tested only on 5.7+, but in theory should also work on 5.6
        if (Ar.Game < Versions.EGame.GAME_UE5_6) return;
        InternalVersion = Ar.Read<long>();
        if (InternalVersion != -1)
            Model = new FModel(new FMutableArchive(Ar));
        
        Private = GetOrDefault<FPackageIndex>(nameof(Private));
    }

    protected internal override void WriteJson(JsonWriter writer, JsonSerializer serializer)
    {
        base.WriteJson(writer, serializer);
        writer.WritePropertyName(nameof(InternalVersion));
        writer.WriteValue(InternalVersion);
        writer.WritePropertyName(nameof(Model));
        serializer.Serialize(writer, Model);
    }
    
    public void ReadByteCode()
    {
        if (Model == null) return;
        var bytecodeReader = new FByteArchive("Mutable ByteCode", Model.Program.ByteCode);
        foreach (var address in Model.Program.OpAddress)
        {
            bytecodeReader.Position = address;
            var opType = bytecodeReader.Read<EOpType>();
        }
    }
}

public enum EOpType : ushort
{
    /** No operation. */
    NONE,

    //-----------------------------------------------------------------------------------------
    // Generic operations
    //-----------------------------------------------------------------------------------------

    //! Constant value
    BO_CONSTANT,
    NU_CONSTANT,
    SC_CONSTANT,
    CO_CONSTANT,
    IM_CONSTANT,
    ME_CONSTANT,
    LA_CONSTANT,
    PR_CONSTANT,
    ST_CONSTANT,
    ED_CONSTANT,
    MA_CONSTANT,
    MI_CONSTANT,

    //! User parameter
    BO_PARAMETER,
    NU_PARAMETER,
    SC_PARAMETER,
    CO_PARAMETER,
    PR_PARAMETER,
    IM_PARAMETER,
    SK_PARAMETER,
    ST_PARAMETER,
    MA_PARAMETER,
    MI_PARAMETER,
    IS_PARAMETER,

    //! A referenced, but opaque engine resource
    IM_REFERENCE,
    ME_REFERENCE,

    //! Select one value or the other depending on a boolean input
    NU_CONDITIONAL,
    SC_CONDITIONAL,
    CO_CONDITIONAL,
    IM_CONDITIONAL,
    ME_CONDITIONAL,
    LA_CONDITIONAL,
    IN_CONDITIONAL,
    ED_CONDITIONAL,
    MI_CONDITIONAL,

    //! Select one of several values depending on an int input
    NU_SWITCH,
    SC_SWITCH,
    CO_SWITCH,
    IM_SWITCH,
    ME_SWITCH,
    LA_SWITCH,
    IN_SWITCH,
    ED_SWITCH,
    MI_SWITCH,

    //! Selects a parameter value from a Material
    SC_MATERIAL_BREAK,
    CO_MATERIAL_BREAK,
    IM_MATERIAL_BREAK,

    //! Converts a texture of a material parameter into a texture parameter to process it at runtime.
    IM_PARAMETER_FROM_MATERIAL,

    //-----------------------------------------------------------------------------------------
    // Boolean operations
    //-----------------------------------------------------------------------------------------

    //! Compare an integerexpression with an integer constant
    BO_EQUAL_INT_CONST,

    //! Logical and
    BO_AND,

    //! Logical or
    BO_OR,

    //! Left as an exercise to the reader to find out what this op does.
    BO_NOT,

    //-----------------------------------------------------------------------------------------
    // Scalar operations
    //-----------------------------------------------------------------------------------------

    //! Apply an arithmetic operation to two scalars
    SC_ARITHMETIC,

    //! Get a scalar value from a curve
    SC_CURVE,

    //! External operation
    SC_EXTERNAL,

    //-----------------------------------------------------------------------------------------
    // Colour operations. Colours are sometimes used as generic vectors.
    //-----------------------------------------------------------------------------------------

    //! Sample an image to get its colour.
    CO_SAMPLEIMAGE,

    //! Make a color by shuffling channels from other colours.
    CO_SWIZZLE,

    //! Compose a vector from 4 scalars
    CO_FROMSCALARS,

    //! Apply component-wise arithmetic operations to two colours
    CO_ARITHMETIC,

    //! Apply a Linear to sRGB color transformation on a given color vector.
    CO_LINEARTOSRGB,

    //! External operation
    CO_EXTERNAL,
    
    //-----------------------------------------------------------------------------------------
    // Image operations
    //-----------------------------------------------------------------------------------------

    //! Combine an image on top of another one using a specific effect (Blend, SoftLight, 
    //! Hardlight, Burn...). And optionally a mask.
    IM_LAYER,

    //! Apply a colour on top of an image using a specific effect (Blend, SoftLight, 
    //! Hardlight, Burn...), optionally using a mask.
    IM_LAYERCOLOUR,        

    //! Convert between pixel formats
    IM_PIXELFORMAT,

    //! Generate mipmaps up to a provided level
    IM_MIPMAP,

    //! Resize the image to a constant size
    IM_RESIZE,

    //! Resize the image to the size of another image
    IM_RESIZELIKE,

    //! Resize the image by a relative factor
    IM_RESIZEREL,

    //! Create an empty image to hold a particular layout.
    IM_BLANKLAYOUT,

    //! Copy an image into a rect of another one.
    IM_COMPOSE,

    //! Interpolate between 2 images taken from a row of targets (2 consecutive targets).
    IM_INTERPOLATE,

    //! Change the saturation of the image.
    IM_SATURATE,

    //! Generate a one-channel image with the luminance of the source image.
    IM_LUMINANCE,

    //! Recombine the channels of several images into one.
    IM_SWIZZLE,

    //! Convert the source image colours using a "palette" image sampled with the source
    //! grey-level.
    IM_COLOURMAP,

    //! Generate a black and white image from an image and a threshold.
    IM_BINARISE,

    //! Generate a plain colour image
    IM_PLAINCOLOUR,

    //! Cut a rect from an image
    IM_CROP,

    //! Replace a subrect of an image with another one
    IM_PATCH,

    //! Render a mesh texture layout into a mask
    IM_RASTERMESH,

    //! Create an image displacement encoding the grow operation for a mask
    IM_MAKEGROWMAP,

    //! Apply an image displacement on another image.
    IM_DISPLACE,

    //! Repeately apply
    IM_MULTILAYER,

    //! Inverts the colors of an image
    IM_INVERT,

    //! Modifiy roughness channel of an image based on normal variance.
    IM_NORMALCOMPOSITE,

    //! Apply linear transform to Image content. Resulting samples outside the original image are tiled.
    IM_TRANSFORM,

    /** Convert an FImage in Passthrough Parameter mode to an FImage with data. */
    IM_PARAMETER_CONVERT,

    //! External operation
    IM_EXTERNAL,
    
    //-----------------------------------------------------------------------------------------
    // Mesh operations
    //-----------------------------------------------------------------------------------------

    //! Apply a layout to a mesh texture coordinates channel
    ME_APPLYLAYOUT,

    /** */
    ME_PREPARELAYOUT,

    //! Compare two meshes and extract a morph from the first to the second
    //! The meshes must have the same topology, etc.
    ME_DIFFERENCE,

    //! Apply a one morphs on a base. 
    ME_MORPH,

    //! Merge a mesh to a mesh
    ME_MERGE,

    //! Create a new mask mesh selecting all the faces of a source that are inside a given
    //! clip mesh.
    ME_MASKCLIPMESH,

    /** Create a new mask mesh selecting the faces of a source that have UVs inside the region marked in an image mask. */
    ME_MASKCLIPUVMASK,

    //! Create a new mask mesh selecting all the faces of a source that match another mesh.
    ME_MASKDIFF,

    //! Remove all the geometry selected by a mask.
    ME_REMOVEMASK,

    //! Change the mesh format to match the format of another one.
    ME_FORMAT,

    //! Extract a fragment of a mesh containing specific layout blocks.
    ME_EXTRACTLAYOUTBLOCK,

    //! Apply a transform in a 4x4 matrix to the geometry channels of the mesh
    ME_TRANSFORM,

    //! Clip the mesh with a plane and morph it when it is near until it becomes an ellipse on
    //! the plane.
    ME_CLIPMORPHPLANE,

    //! Clip the mesh with another mesh.
    ME_CLIPWITHMESH,

    //! Replace the skeleton data from a mesh with another one.
    ME_SETSKELETON,

    //! Project a mesh using a projector and clipping the irrelevant faces
    ME_PROJECT,

    //! Deform a skinned mesh applying a skeletal pose
    ME_APPLYPOSE,

    //! Calculate the binding of a mesh on a shape
    ME_BINDSHAPE,

    //! Apply a shape on a (previously bound) mesh
    ME_APPLYSHAPE,

    //! Clip Deform using bind data.
    ME_CLIPDEFORM,

    //! Mesh morph with Skeleton Reshape based on the morphed mesh.
    ME_MORPHRESHAPE,

    //! Optimize skinning before adding a mesh to the component
    ME_OPTIMIZESKINNING,

    //! Add a metadata to a mesh
    ME_ADDMETADATA,

    //! Transform with a 4x4 matrix the geometry channels of a mesh that are bounded by another mesh
    ME_TRANSFORMWITHMESH,

    //! Transform with a 4x4 matrix the geometry channels of a mesh that are skinned to a bone or hierarchy
    ME_TRANSFORMWITHBONE,

    //! External operation
    ME_EXTERNAL,

    /** Select a Mesh Section from a Skeletal Mesh. */
    ME_SKELETALMESH_BREAK,

    //-----------------------------------------------------------------------------------------
    // Instance operations
    //-----------------------------------------------------------------------------------------

    //! Add a mesh to an instance
    IN_ADDMESH,

    //! Add an image to an instance
    IN_ADDIMAGE,

    //! Add a vector to an instance
    IN_ADDVECTOR,

    //! Add a scalar to an instance
    IN_ADDSCALAR,

    //! Add a string to an instance
    IN_ADDSTRING,

    //! Add a surface to an instance component
    IN_ADDSURFACE,

    //! Add a component to an instance LOD
    IN_ADDCOMPONENT,

    //! Add all LODs to an instance. This operation can only appear once in a model.
    IN_ADDLOD,

    //! Add a Skeletal Mesh to an instance.
    IN_ADDSKELETALMESH,

    //! Add extension data to an instance
    IN_ADDEXTENSIONDATA,

    //! Add overlay material to an instance
    IN_ADDOVERLAYMATERIAL,

    //! Add override material to an instance
    IN_ADDOVERRIDEMATERIAL,

    //! Add a material to an instance
    IN_ADDMATERIAL,

    //-----------------------------------------------------------------------------------------
    // Layout operations
    //-----------------------------------------------------------------------------------------

    //! Pack all the layout blocks from the source in the grid without overlapping
    LA_PACK,

    //! Merge two layouts
    LA_MERGE,

    //! Remove all layout blocks not used by any vertex of the mesh.
    //! This operation is for the new way of managing layout blocks.
    LA_REMOVEBLOCKS,

    //! Extract a layout from a mesh
    LA_FROMMESH,

    //-----------------------------------------------------------------------------------------
    // Material operations
    //-----------------------------------------------------------------------------------------

    //! External operation
    MI_EXTERNAL,

    // Get a material from a given skeletal mesh
    MI_SKELETALMESH_BREAK,
    
    //-----------------------------------------------------------------------------------------
    // FInstancedStruct operations
    //-----------------------------------------------------------------------------------------

    //! External operation
    IS_EXTERNAL,
    
    //-----------------------------------------------------------------------------------------
    // Utility values
    //-----------------------------------------------------------------------------------------

    //!
    COUNT
}
