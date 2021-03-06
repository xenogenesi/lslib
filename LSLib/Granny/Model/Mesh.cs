﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Collada141;
using OpenTK;
using LSLib.Granny.GR2;

namespace LSLib.Granny.Model
{
    public class VertexDeduplicator
    {
        public Dictionary<int, int> VertexDeduplicationMap = new Dictionary<int, int>();
        public List<Dictionary<int, int>> UVDeduplicationMaps = new List<Dictionary<int, int>>();
        public List<Vertex> DeduplicatedPositions = new List<Vertex>();
        public List<List<Vector2>> DeduplicatedUVs = new List<List<Vector2>>();

        private class VertexPositionComparer : IEqualityComparer<Vector3>
        {
            public bool Equals(Vector3 a, Vector3 b)
            {
                return a.X == b.X && a.Y == b.Y && a.Z == b.Z;
            }

            public int GetHashCode(Vector3 v)
            {
                int hash = 17;
                hash = hash * 23 + v.X.GetHashCode();
                hash = hash * 23 + v.Y.GetHashCode();
                hash = hash * 23 + v.Z.GetHashCode();
                return hash;
            }
        }

        private class VertexUVComparer : IEqualityComparer<Vector2>
        {
            public bool Equals(Vector2 a, Vector2 b)
            {
                return a.X == b.X && a.Y == b.Y;
            }

            public int GetHashCode(Vector2 v)
            {
                int hash = 17;
                hash = hash * 23 + v.X.GetHashCode();
                hash = hash * 23 + v.Y.GetHashCode();
                return hash;
            }
        }

        public void MakeIdentityMapping(List<Vertex> vertices)
        {
            for (var i = 0; i < vertices.Count; i++)
            {
                DeduplicatedPositions.Add(vertices[i]);
                VertexDeduplicationMap.Add(i, i);
            }

            var numUvs = Vertex.Description(vertices[0].GetType()).TextureCoordinates;
            for (var uv = 0; uv < numUvs; uv++)
            {
                var uvMap = new Dictionary<int, int>();
                var deduplicatedUvs = new List<Vector2>();
                UVDeduplicationMaps.Add(uvMap);
                DeduplicatedUVs.Add(deduplicatedUvs);

                for (var i = 0; i < vertices.Count; i++)
                {
                    deduplicatedUvs.Add(vertices[i].GetTextureCoordinates(uv));
                    uvMap.Add(i, i);
                }
            }
        }

        public void Deduplicate(List<Vertex> vertices)
        {
            var positions = new Dictionary<Vector3, int>(new VertexPositionComparer());
            for (var i = 0; i < vertices.Count; i++)
            {
                int mappedIndex;
                if (!positions.TryGetValue(vertices[i].Position, out mappedIndex))
                {
                    mappedIndex = positions.Count;
                    positions.Add(vertices[i].Position, mappedIndex);
                    DeduplicatedPositions.Add(vertices[i]);
                }

                VertexDeduplicationMap.Add(i, mappedIndex);
            }

            var numUvs = Vertex.Description(vertices[0].GetType()).TextureCoordinates;
            for (var uv = 0; uv < numUvs; uv++)
            {
                var uvMap = new Dictionary<int, int>();
                var deduplicatedUvs = new List<Vector2>();
                UVDeduplicationMaps.Add(uvMap);
                DeduplicatedUVs.Add(deduplicatedUvs);

                var uvs = new Dictionary<Vector2, int>(new VertexUVComparer());
                for (var i = 0; i < vertices.Count; i++)
                {
                    int mappedIndex;
                    if (!uvs.TryGetValue(vertices[i].GetTextureCoordinates(uv), out mappedIndex))
                    {
                        mappedIndex = uvs.Count;
                        uvs.Add(vertices[i].GetTextureCoordinates(uv), mappedIndex);
                        deduplicatedUvs.Add(vertices[i].GetTextureCoordinates(uv));
                    }

                    uvMap.Add(i, mappedIndex);
                }
            }
        }
    }

    public class VertexAnnotationSet
    {
        public string Name;
        [Serialization(Type = MemberType.ReferenceToVariantArray)]
        public List<object> VertexAnnotations;
        public Int32 IndicesMapFromVertexToAnnotation;
        public List<TriIndex> VertexAnnotationIndices;
    }

    public class VertexData
    {
        [Serialization(Type = MemberType.ReferenceToVariantArray, SectionSelector = typeof(VertexSerializer),
            TypeSelector = typeof(VertexSerializer), Serializer = typeof(VertexSerializer),
            Kind = SerializationKind.UserElement)]
        public List<Vertex> Vertices;
        public List<GrannyString> VertexComponentNames;
        public List<VertexAnnotationSet> VertexAnnotationSets;
        [Serialization(Kind = SerializationKind.None)]
        public VertexDeduplicator Deduplicator;

        public void PostLoad()
        {
            // Fix missing vertex component names
            if (VertexComponentNames == null)
            {
                VertexComponentNames = new List<GrannyString>();
                if (Vertices.Count > 0)
                {
                    var components = Vertices[0].ComponentNames();
                    foreach (var name in components)
                    {
                        VertexComponentNames.Add(new GrannyString(name));
                    }
                }
            }
        }

        public void Deduplicate()
        {
            Deduplicator = new VertexDeduplicator();
            Deduplicator.Deduplicate(Vertices);
        }

        private void EnsureDeduplicationMap()
        {
            // Makes sure that we have an original -> duplicate vertex index map to work with.
            // If we don't, it creates an identity mapping between the original and the Collada vertices.
            // To deduplicate GR2 vertex data, Deduplicate() should be called before any Collada export call.
            if (Deduplicator == null)
            {
                Deduplicator = new VertexDeduplicator();
                Deduplicator.MakeIdentityMapping(Vertices);
            }
        }

        public source MakeColladaPositions(string name)
        {
            EnsureDeduplicationMap();

            int index = 0;
            var positions = new float[Deduplicator.DeduplicatedPositions.Count * 3];
            foreach (var vertex in Deduplicator.DeduplicatedPositions)
            {
                var pos = vertex.Position;
                positions[index++] = pos[0];
                positions[index++] = pos[1];
                positions[index++] = pos[2];
            }

            return ColladaUtils.MakeFloatSource(name, "positions", new string[] { "X", "Y", "Z" }, positions);
        }

        public source MakeColladaNormals(string name)
        {
            EnsureDeduplicationMap();

            int index = 0;
            var normals = new float[Deduplicator.DeduplicatedPositions.Count * 3];
            foreach (var vertex in Deduplicator.DeduplicatedPositions)
            {
                var normal = vertex.Normal;
                normals[index++] = normal[0];
                normals[index++] = normal[1];
                normals[index++] = normal[2];
            }

            return ColladaUtils.MakeFloatSource(name, "normals", new string[] { "X", "Y", "Z" }, normals);
        }

        public source MakeColladaTangents(string name)
        {
            EnsureDeduplicationMap();

            int index = 0;
            var tangents = new float[Deduplicator.DeduplicatedPositions.Count * 3];
            foreach (var vertex in Deduplicator.DeduplicatedPositions)
            {
                var tangent = vertex.Tangent;
                tangents[index++] = tangent[0];
                tangents[index++] = tangent[1];
                tangents[index++] = tangent[2];
            }

            return ColladaUtils.MakeFloatSource(name, "tangents", new string[] { "X", "Y", "Z" }, tangents);
        }

        public source MakeColladaBinormals(string name)
        {
            EnsureDeduplicationMap();

            int index = 0;
            var binormals = new float[Deduplicator.DeduplicatedPositions.Count * 3];
            foreach (var vertex in Deduplicator.DeduplicatedPositions)
            {
                var binormal = vertex.Binormal;
                binormals[index++] = binormal[0];
                binormals[index++] = binormal[1];
                binormals[index++] = binormal[2];
            }

            return ColladaUtils.MakeFloatSource(name, "binormals", new string[] { "X", "Y", "Z" }, binormals);
        }

        public source MakeColladaUVs(string name, int uvIndex)
        {
            EnsureDeduplicationMap();

            int index = 0;
            var uvs = new float[Deduplicator.DeduplicatedUVs[uvIndex].Count * 2];
            foreach (var uv in Deduplicator.DeduplicatedUVs[uvIndex])
            {
                uvs[index++] = uv[0];
                uvs[index++] = uv[1];
            }

            return ColladaUtils.MakeFloatSource(name, "uvs" + uvIndex.ToString(), new string[] { "S", "T" }, uvs);
        }

        public source MakeBoneWeights(string name)
        {
            EnsureDeduplicationMap();

            var weights = new List<float>(Deduplicator.DeduplicatedPositions.Count);
            foreach (var vertex in Deduplicator.DeduplicatedPositions)
            {
                var boneWeights = vertex.BoneWeights;
                for (int i = 0; i < 4; i++)
                {
                    if (boneWeights[i] > 0)
                        weights.Add(boneWeights[i] / 255.0f);
                }
            }

            return ColladaUtils.MakeFloatSource(name, "weights", new string[] { "WEIGHT" }, weights.ToArray());
        }

        public void Transform(Matrix4 transformation)
        {
            foreach (var vertex in Vertices)
            {
                vertex.Transform(transformation);
            }
        }
    }

    public class TriTopologyGroup
    {
        public int MaterialIndex;
        public int TriFirst;
        public int TriCount;
    }

    public class TriIndex
    {
        public Int32 Int32;
    }

    public class TriIndex16
    {
        public Int16 Int16;
    }

    public class TriAnnotationSet
    {
        public string Name;
        [Serialization(Type = MemberType.ReferenceToVariantArray)]
        public object TriAnnotations;
        public Int32 IndicesMapFromTriToAnnotation;
        [Serialization(Section = SectionType.RigidIndex, Prototype = typeof(TriIndex), Kind = SerializationKind.UserMember, Serializer = typeof(Int32ListSerializer))]
        public List<Int32> TriAnnotationIndices;
    }

    public class TriTopology
    {
        public List<TriTopologyGroup> Groups;
        [Serialization(Section = SectionType.DeformableIndex, Prototype = typeof(TriIndex), Kind = SerializationKind.UserMember, Serializer = typeof(Int32ListSerializer))]
        public List<Int32> Indices;
        [Serialization(Section = SectionType.DeformableIndex, Prototype = typeof(TriIndex16), Kind = SerializationKind.UserMember, Serializer = typeof(Int16ListSerializer))]
        public List<Int16> Indices16;
        [Serialization(Section = SectionType.DeformableIndex, Prototype = typeof(TriIndex), Kind = SerializationKind.UserMember, Serializer = typeof(Int32ListSerializer))]
        public List<Int32> VertexToVertexMap;
        [Serialization(Section = SectionType.DeformableIndex, Prototype = typeof(TriIndex), Kind = SerializationKind.UserMember, Serializer = typeof(Int32ListSerializer))]
        public List<Int32> VertexToTriangleMap;
        [Serialization(Section = SectionType.DeformableIndex, Prototype = typeof(TriIndex), Kind = SerializationKind.UserMember, Serializer = typeof(Int32ListSerializer))]
        public List<Int32> SideToNeighborMap;
        [Serialization(Section = SectionType.DeformableIndex, Prototype = typeof(TriIndex), Kind = SerializationKind.UserMember, Serializer = typeof(Int32ListSerializer), MinVersion = 0x80000038)]
        public List<Int32> PolygonIndexStarts;
        [Serialization(Section = SectionType.DeformableIndex, Prototype = typeof(TriIndex), Kind = SerializationKind.UserMember, Serializer = typeof(Int32ListSerializer), MinVersion = 0x80000038)]
        public List<Int32> PolygonIndices;
        [Serialization(Section = SectionType.DeformableIndex, Prototype = typeof(TriIndex), Kind = SerializationKind.UserMember, Serializer = typeof(Int32ListSerializer))]
        public List<Int32> BonesForTriangle;
        [Serialization(Section = SectionType.DeformableIndex, Prototype = typeof(TriIndex), Kind = SerializationKind.UserMember, Serializer = typeof(Int32ListSerializer))]
        public List<Int32> TriangleToBoneIndices;
        public List<TriAnnotationSet> TriAnnotationSets;

        public void PostLoad()
        {
            // Convert 16-bit vertex indices to 32-bit indices
            // (for convenience, so we won't have to handle both Indices and Indices16 in all code paths)
            if (Indices16 != null)
            {
                Indices = new List<Int32>(Indices16.Count);
                foreach (var index in Indices16)
                {
                    Indices.Add(index);
                }

                Indices16 = null;
            }
        }

        public triangles MakeColladaTriangles(InputLocalOffset[] inputs, Dictionary<int, int> vertexMaps, List<Dictionary<int, int>> uvMaps)
        {
            int numTris = (from grp in Groups
                           select grp.TriCount).Sum();

            var tris = new triangles();
            tris.count = (ulong)numTris;
            tris.input = inputs;

            var indicesBuilder = new StringBuilder();
            foreach (var group in Groups)
            {
                var indices = Indices;
                for (int index = group.TriFirst; index < group.TriFirst + group.TriCount; index++)
                {
                    int firstIdx = index * 3;
                    for (int vertIndex = 0; vertIndex < 3; vertIndex++)
                    {
                        for (int i = 0; i < inputs.Length; i++)
                        {
                            // TODO: Hacky!
                            if (i == 0)
                                indicesBuilder.Append(vertexMaps[indices[firstIdx + vertIndex]]);
                            else
                                indicesBuilder.Append(uvMaps[i - 1][indices[firstIdx + vertIndex]]);
                            indicesBuilder.Append(" ");
                        }
                    }
                }
            }

            tris.p = indicesBuilder.ToString();
            return tris;
        }
    }

    public class BoneBinding
    {
        public string BoneName;
        [Serialization(ArraySize = 3)]
        public float[] OBBMin;
        [Serialization(ArraySize = 3)]
        public float[] OBBMax;
        [Serialization(Section = SectionType.DeformableIndex, Prototype = typeof(TriIndex), Kind = SerializationKind.UserMember, Serializer = typeof(Int32ListSerializer))]
        public List<Int32> TriangleIndices;
    }
    
    public class MaterialReference
    {
        public string Usage;
        public Material Map;
    }

    public class TextureLayout
    {
        public Int32 BytesPerPixel;
        [Serialization(ArraySize = 4)]
        public Int32[] ShiftForComponent;
        [Serialization(ArraySize = 4)]
        public Int32[] BitsForComponent;
    }

    public class PixelByte
    {
        public Byte UInt8;
    }

    public class TextureMipLevel
    {
        public Int32 Stride;
        public List<PixelByte> PixelBytes;
    }

    public class TextureImage
    {
        public List<TextureMipLevel> MIPLevels;
    }

    public class Texture
    {
        public string FromFileName;
        public Int32 TextureType;
        public Int32 Width;
        public Int32 Height;
        public Int32 Encoding;
        public Int32 SubFormat;
        [Serialization(Type = MemberType.Inline)]
        public TextureLayout Layout;
        public List<TextureImage> Images;
        public object ExtendedData;
    }

    public class Material
    {
        public string Name;
        public List<MaterialReference> Maps;
        public Texture Texture;
        public object ExtendedData;
    }

    public class MaterialBinding
    {
        public Material Material;
    }

    public class MorphTarget
    {
        public string ScalarName;
        public VertexData VertexData;
        public Int32 DataIsDeltas;
    }

    public class Mesh
    {
        public string Name;
        public VertexData PrimaryVertexData;
        public List<MorphTarget> MorphTargets;
        public TriTopology PrimaryTopology;
        [Serialization(DataArea = true)]
        public List<MaterialBinding> MaterialBindings;
        public List<BoneBinding> BoneBindings;
        [Serialization(Type = MemberType.VariantReference)]
        public object ExtendedData;

        [Serialization(Kind = SerializationKind.None)]
        public Dictionary<int, List<int>> OriginalToConsolidatedVertexIndexMap;

        [Serialization(Kind = SerializationKind.None)]
        public Type VertexFormat;

        public static Mesh ImportFromCollada(mesh mesh, string vertexFormat, bool rebuildNormals = false, bool rebuildTangents = false)
        {
            var collada = new ColladaMesh();
            collada.ImportFromCollada(mesh, vertexFormat, rebuildNormals, rebuildTangents);

            var m = new Mesh();
            m.VertexFormat = VertexFormatRegistry.Resolve(vertexFormat);
            m.Name = "Unnamed";

            m.PrimaryVertexData = new VertexData();
            var components = new List<GrannyString>();
            components.Add(new GrannyString("Position"));

            var vertexDesc = Vertex.Description(m.VertexFormat);
            if (vertexDesc.BoneWeights)
            {
                components.Add(new GrannyString("BoneWeights"));
                components.Add(new GrannyString("BoneIndices"));
            }

            components.Add(new GrannyString("Normal"));
            components.Add(new GrannyString("Tangent"));
            components.Add(new GrannyString("Binormal"));
            components.Add(new GrannyString("MaxChannel_1"));
            m.PrimaryVertexData.VertexComponentNames = components;
            m.PrimaryVertexData.Vertices = collada.ConsolidatedVertices;

            m.PrimaryTopology = new TriTopology();
            m.PrimaryTopology.Indices = collada.ConsolidatedIndices;
            m.PrimaryTopology.Groups = new List<TriTopologyGroup>();
            var triGroup = new TriTopologyGroup();
            triGroup.MaterialIndex = 0;
            triGroup.TriFirst = 0;
            triGroup.TriCount = collada.TriangleCount;
            m.PrimaryTopology.Groups.Add(triGroup);

            m.MaterialBindings = new List<MaterialBinding>();
            m.MaterialBindings.Add(new MaterialBinding());

            // m.BoneBindings; - TODO

            m.OriginalToConsolidatedVertexIndexMap = collada.OriginalToConsolidatedVertexIndexMap;
            Utils.Info(String.Format("Imported {0} mesh ({1} tri groups, {2} tris)", (vertexDesc.BoneWeights ? "skinned" : "rigid"), m.PrimaryTopology.Groups.Count, collada.TriangleCount));

            return m;
        }

        public void PostLoad()
        {
            if (PrimaryVertexData.Vertices.Count > 0)
            {
                VertexFormat = PrimaryVertexData.Vertices[0].GetType();
            }
        }

        public mesh ExportToCollada(ExporterOptions options)
        {
            // TODO: model transform/inverse transform?

            source vertexSource = null;
            var sources = new List<source>();
            ulong inputOffset = 0;
            var inputs = new List<InputLocal>();
            var inputOffsets = new List<InputLocalOffset>();
            foreach (var component in PrimaryVertexData.VertexComponentNames)
            {
                var input = new InputLocal();
                source source = null;
                switch (component.String)
                {
                    case "Position":
                        {
                            source = PrimaryVertexData.MakeColladaPositions(Name);
                            vertexSource = source;
                            input.semantic = "POSITION";

                            var vertexInputOff = new InputLocalOffset();
                            vertexInputOff.semantic = "VERTEX";
                            vertexInputOff.source = "#" + source.id;
                            vertexInputOff.offset = inputOffset++;
                            inputOffsets.Add(vertexInputOff);
                            break;
                        }

                    case "Normal":
                        {
                            if (options.ExportNormals)
                            {
                                source = PrimaryVertexData.MakeColladaNormals(Name);
                                input.semantic = "NORMAL";
                            }
                            break;
                        }

                    case "Tangent":
                        {
                            if (options.ExportTangents)
                            {
                                source = PrimaryVertexData.MakeColladaTangents(Name);
                                input.semantic = "TANGENT";
                            }
                            break;
                        }

                    case "Binormal":
                        {
                            if (options.ExportTangents)
                            {
                                source = PrimaryVertexData.MakeColladaBinormals(Name);
                                input.semantic = "BINORMAL";
                            }
                            break;
                        }

                    case "MaxChannel_1":
                    case "MaxChannel_2":
                    case "UVChannel_1":
                    case "UVChannel_2":
                        {
                            if (options.ExportUVs)
                            {
                                int uvIndex = Int32.Parse(component.String.Substring(11)) - 1;
                                source = PrimaryVertexData.MakeColladaUVs(Name, uvIndex);

                                var texInputOff = new InputLocalOffset();
                                texInputOff.semantic = "TEXCOORD";
                                texInputOff.source = "#" + source.id;
                                texInputOff.offset = inputOffset++;
                                inputOffsets.Add(texInputOff);
                            }
                            break;
                        }

                    case "BoneWeights":
                    case "BoneIndices":
                        // These are handled in ExportSkin()
                        break;

                    case "DiffuseColor0":
                    case "map1": // Possibly bogus D:OS name for DiffuseColor0
                        // TODO: This is not exported at the moment.
                        break;

                    default:
                        throw new NotImplementedException("Vertex component not supported: " + component.String);
                }

                if (source != null)
                    sources.Add(source);

                if (input.semantic != null)
                {
                    input.source = "#" + source.id;
                    inputs.Add(input);
                }
            }

            var triangles = PrimaryTopology.MakeColladaTriangles(
                inputOffsets.ToArray(), 
                PrimaryVertexData.Deduplicator.VertexDeduplicationMap,
                PrimaryVertexData.Deduplicator.UVDeduplicationMaps
            );

            var colladaMesh = new mesh();
            colladaMesh.vertices = new vertices();
            colladaMesh.vertices.id = Name + "-vertices";
            colladaMesh.vertices.input = inputs.ToArray();
            colladaMesh.source = sources.ToArray();
            colladaMesh.Items = new object[] { triangles };

            return colladaMesh;
        }

        public static Matrix4 FloatsToMatrix(float[] items)
        {
            return new Matrix4(
                items[0], items[1], items[2], items[3],
                items[4], items[5], items[6], items[7],
                items[8], items[9], items[10], items[11],
                items[12], items[13], items[14], items[15]
            );
        }

        public bool IsSkinned()
        {
            // Check if we have both the BoneWeights and BoneIndices vertex components.
            bool hasWeights = false, hasIndices = false;
            foreach (var component in PrimaryVertexData.VertexComponentNames)
            {
                if (component.String == "BoneWeights")
                    hasWeights = true;
                else if (component.String == "BoneIndices")
                    hasIndices = true;
            }

            return hasWeights && hasIndices;
        }

        public skin ExportSkin(List<Bone> bones, Dictionary<string, Bone> nameMaps, string geometryId)
        {
            var sources = new List<source>();
            var joints = new List<string>();
            var poses = new List<float>();

            var boundBones = new HashSet<string>();
            var orderedBones = new List<Bone>();
            foreach (var boneBinding in BoneBindings)
            {
                boundBones.Add(boneBinding.BoneName);
                orderedBones.Add(nameMaps[boneBinding.BoneName]);
            }

            /*
             * Append all bones to the end of the bone list, even if they're not influencing the mesh.
             * We need this because some tools (eg. Blender) expect all bones to be present, otherwise their
             * inverse world transform would reset to identity.
             */
            foreach (var bone in bones)
            {
                if (!boundBones.Contains(bone.Name))
                {
                    orderedBones.Add(bone);
                }
            }

            foreach (var bone in orderedBones)
            {
                boundBones.Add(bone.Name);
                joints.Add(bone.Name);

                var invWorldTransform = FloatsToMatrix(bone.InverseWorldTransform);
                invWorldTransform.Transpose();

                poses.AddRange(new float[] {
                    invWorldTransform.M11, invWorldTransform.M12, invWorldTransform.M13, invWorldTransform.M14,
                    invWorldTransform.M21, invWorldTransform.M22, invWorldTransform.M23, invWorldTransform.M24,
                    invWorldTransform.M31, invWorldTransform.M32, invWorldTransform.M33, invWorldTransform.M34,
                    invWorldTransform.M41, invWorldTransform.M42, invWorldTransform.M43, invWorldTransform.M44
                });
            }

            var jointSource = ColladaUtils.MakeNameSource(Name, "joints", new string[] { "JOINT" }, joints.ToArray());
            var poseSource = ColladaUtils.MakeFloatSource(Name, "poses", new string[] { "TRANSFORM" }, poses.ToArray(), 16, "float4x4");
            var weightsSource = PrimaryVertexData.MakeBoneWeights(Name);

            var vertices = PrimaryVertexData.Deduplicator.DeduplicatedPositions;
            var vertexInfluenceCounts = new List<int>(vertices.Count);
            var vertexInfluences = new List<int>(vertices.Count);
            int weightIdx = 0;
            foreach (var vertex in vertices)
            {
                int influences = 0;
                var indices = vertex.BoneIndices;
                var weights = vertex.BoneWeights;
                for (int i = 0; i < 4; i++)
                {
                    if (weights[i] > 0)
                    {
                        influences++;
                        vertexInfluences.Add(indices[i]);
                        vertexInfluences.Add(weightIdx++);
                    }
                }

                vertexInfluenceCounts.Add(influences);
            }

            var jointOffsets = new InputLocalOffset();
            jointOffsets.semantic = "JOINT";
            jointOffsets.source = "#" + jointSource.id;
            jointOffsets.offset = 0;

            var weightOffsets = new InputLocalOffset();
            weightOffsets.semantic = "WEIGHT";
            weightOffsets.source = "#" + weightsSource.id;
            weightOffsets.offset = 1;

            var vertWeights = new skinVertex_weights();
            vertWeights.count = (ulong)vertices.Count;
            vertWeights.input = new InputLocalOffset[] { jointOffsets, weightOffsets };
            vertWeights.v = string.Join(" ", vertexInfluences.Select(x => x.ToString()).ToArray());
            vertWeights.vcount = string.Join(" ", vertexInfluenceCounts.Select(x => x.ToString()).ToArray());

            var skin = new skin();
            skin.source1 = "#" + geometryId;
            skin.bind_shape_matrix = "1 0 0 0 0 1 0 0 0 0 1 0 0 0 0 1";

            var skinJoints = new skinJoints();
            var skinJointInput = new InputLocal();
            skinJointInput.semantic = "JOINT";
            skinJointInput.source = "#" + jointSource.id;
            var skinInvBindInput = new InputLocal();
            skinInvBindInput.semantic = "INV_BIND_MATRIX";
            skinInvBindInput.source = "#" + poseSource.id;
            skinJoints.input = new InputLocal[] { skinJointInput, skinInvBindInput };

            skin.joints = skinJoints;
            skin.source = new source[] { jointSource, poseSource, weightsSource };
            skin.vertex_weights = vertWeights;

            return skin;
        }
    }
}
