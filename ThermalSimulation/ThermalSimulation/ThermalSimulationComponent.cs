using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.DocObjects;
using Rhino.DocObjects.Custom;
using Rhino.Geometry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;


// In order to load the result of this wizard, you will also need to
// add the output bin/ folder of this project to the list of loaded
// folder in Grasshopper.
// You can use the _GrasshopperDeveloperSettings Rhino command for that.

namespace ThermalSimulation
{
    public class ThermalSimulationComponent : GH_Component
    {
        [DllImport("thermal_renderer_lib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int test_lib(int _value);

        [DllImport("thermal_renderer_lib.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int test_fill_array([In, Out] float[] _arr, int _size);

        [DllImport("thermal_renderer_lib.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int load(bool _withConsole);

        [DllImport("thermal_renderer_lib.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int unload();

        [DllImport("thermal_renderer_lib.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int load_geometry(
            [In] float[] _vertices,
            [In, Out] float[] _vertex_colors,
            uint _vertex_count,
            [In] uint[] _indices,
            uint _indices_count,
           [In] ObjectProperties[] _object_properties,
            uint _object_count
            );

        [DllImport("thermal_renderer_lib.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int load_sky(
            [In] float[] _vertices,
            uint _vertex_count,
            [In] uint[] _indices,
            [In] float[] _values,
            uint _triangle_count
            );

        [DllImport("thermal_renderer_lib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int unload_scene();

        [DllImport("thermal_renderer_lib.dll", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int update_sky_values(
          [In] float[] _values,
          uint _quad_count
          );

        [DllImport("thermal_renderer_lib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int simulate(
            float _step_size_hours,
            uint _time_step_count,
            [In, Out] ref float _time_hours);

        [DllImport("thermal_renderer_lib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int reset_simulation();

        [DllImport("thermal_renderer_lib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int get_vertex_temperatures(
            [In, Out] float[] _vertex_temperatures,
            uint _total_vertex_count);

        [DllImport("thermal_renderer_lib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int get_vertex_values(
            [In, Out] float[] _vertex_temperatures,
            uint _total_vertex_count,
            uint _type);

        [DllImport("thermal_renderer_lib.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern int set_steady_state(bool _enabled);

        [StructLayout(LayoutKind.Sequential)]
        public struct ObjectProperties
        {
            public uint vertex_offset;
            public uint vertex_count;
            public uint indices_offset;
            public uint indices_count;

            public float kelvin;
            [MarshalAs(UnmanagedType.U1)]
            public bool temperature_fixed;

            public float thickness;
            public float density;
            public float heat_capacity;
            public float heat_conductivity;

            public float diffuse_reflectance;
            public float specular_reflectance;

            [MarshalAs(UnmanagedType.U1)]
            public bool diffuse_emission;
            [MarshalAs(UnmanagedType.U1)]
            public bool traceable;
        }

        int execution_counter = 0;
        bool showConsole = true;

        bool loadGeometry = true;
        bool loadSkyValues = false;

        int geometry_hash = 0;
        int meshes_hash = 0;
        int sky_values_hash = 0;

        int simulation_step = -1;

        float time_in_hours = 0.0f;

        float[] vertex_colors = null;

        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public ThermalSimulationComponent()
          : base("ThermalSimulation", "ThermalSim",
              "Comutes the thermal simulation.",
              "Simulation", "Thermal")
        {           
        }

        public override void AddedToDocument(GH_Document document)
        {
            load(showConsole);
            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "LIB loaded.");
        }

        public override void RemovedFromDocument(GH_Document document)
        {
            unload();
            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "LIB unloaded.");
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            /*0*/ pManager.AddGeometryParameter("Geometry", "Geometry", "the input geometry", GH_ParamAccess.list);
            /*1*/ pManager.AddMeshParameter("Meshes", "Meshes", "the input mesh", GH_ParamAccess.list);
            /*2*/ pManager.AddBooleanParameter("----------", "----------", "----------", GH_ParamAccess.item, false);
            /*3*/ int ind = pManager.AddMeshParameter("Sky Mesh", "SkyMesh", "sky mesh", GH_ParamAccess.item); pManager[ind].Optional = true;
            /*4*/ ind = pManager.AddNumberParameter("Sky Values", "SkyValues", "sky values", GH_ParamAccess.list); pManager[ind].Optional = true;
            /*5*/ ind = pManager.AddNumberParameter("Sky Values Timespan", "SkyValuesTimespan", "sky values timespan", GH_ParamAccess.item, 1.0); pManager[ind].Optional = true;
            /*6*/ pManager.AddBooleanParameter("----------", "----------", "----------", GH_ParamAccess.item, false);
            /*7*/ pManager.AddBooleanParameter("Reload Geometry", "ReloadGeometry", "reload geometry", GH_ParamAccess.item, false);
            /*8*/ pManager.AddBooleanParameter("Reload Sky Values", "ReloadSkyValues", "reload sky values", GH_ParamAccess.item, false);
            /*9*/ pManager.AddBooleanParameter("----------", "----------", "----------", GH_ParamAccess.item, false);
            /*10*/ pManager.AddNumberParameter("Step Size", "StepSize", "step size in hours", GH_ParamAccess.item, 0.1);
            /*11*/ pManager.AddIntegerParameter("Step Count", "StepCount", "step count", GH_ParamAccess.item, 1);
            /*12*/ pManager.AddBooleanParameter("Simulate", "Simulate", "compute simulation", GH_ParamAccess.item, false);
            /*13*/ pManager.AddBooleanParameter("Reset Simulation", "ResetSimulation", "reset simulation", GH_ParamAccess.item, false);
            /*14*/ ind = pManager.AddIntegerParameter("Simulation Step", "SimulationStep", "simulation step", GH_ParamAccess.item, -1); pManager[ind].Optional = true;
            /*15*/ ind = pManager.AddBooleanParameter("Enable Steady State", "EnableSteadyState", "enable steady state", GH_ParamAccess.item, false); pManager[ind].Optional = true;
            /*16*/ ind = pManager.AddIntegerParameter("Value Type", "ValueType", "set vertex value type", GH_ParamAccess.item, 0); pManager[ind].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "the output mesh", GH_ParamAccess.list);
            pManager.AddNumberParameter("Kelvin", "kelvin", "the kelvin array", GH_ParamAccess.list);
            pManager.AddNumberParameter("Time", "TimeHours", "time in hours", GH_ParamAccess.list);
        }
        
        private int GetSequenceHashCode<T>(IEnumerable<T> sequence)
        {
            const int seed = 487;
            const int modifier = 31;

            unchecked
            {
                return sequence.Aggregate(seed, (current, item) =>
                    (current * modifier) + item.GetHashCode());
            }
        }

        private int GetMeshesHash(List<Mesh> _meshes)
        {
            List<int> counts = new List<int>();

            foreach(Mesh m in _meshes)
            {
                counts.Add(m.Vertices.Count);
                counts.Add(m.Faces.Count);
            }

            return GetSequenceHashCode(counts);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            execution_counter++;

            int input = 2;
            int val = test_lib(2);
            string str = String.Format("test_lib({0}) = {1}", input, val);
            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, str);

            float[] arr = { 0.1f, 0.1f, 0.1f };
            test_fill_array(arr, 3);
            string arr_str = String.Format("fill_array() = [{0}, {1}, {2}]", arr[0], arr[1], arr[2]);
            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, arr_str);

            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, String.Format("exectution counter = {0}", execution_counter));

            bool reload_geometry = false;
            if (!DA.GetData(7, ref reload_geometry)) { return; }

            bool reload_sky_values = false;
            if (!DA.GetData(8, ref reload_sky_values)) { return; }

            List<IGH_GeometricGoo> goos = new List<IGH_GeometricGoo>();
            if (!DA.GetDataList(0, goos)) { return; }
            if (goos.Count == 0) { return; }

            /*
            int new_geometry_hash = GetSequenceHashCode(goos);
            if (geometry_hash != new_geometry_hash)
            {
                reload_geometry = true;
                geometry_hash = new_geometry_hash;
            }
            */

            List<Mesh> meshes = new List<Mesh>();
            if (!DA.GetDataList(1, meshes)) { return; }
            if (meshes.Count == 0) { return; }

            int new_meshes_hash = GetMeshesHash(meshes);
            if (meshes_hash != new_meshes_hash)
            {
                reload_geometry = true;
                meshes_hash = new_meshes_hash;
            }

            Mesh sky_mesh = new Mesh();
            List<GH_Number> sky_mesh_values = new List<GH_Number>();
            double sky_mesh_values_timespan = 1.0f;
            if (DA.GetData(3, ref sky_mesh))
            {
                if (!DA.GetDataList(4, sky_mesh_values))
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Sky Values missing!");
                    return;
                }
                if (!DA.GetData(5, ref sky_mesh_values_timespan))
                {
                    this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Sky Values Timespan missing!");
                    return;
                }

                int new_sky_values_hash = GetSequenceHashCode(sky_mesh_values);
                if(sky_values_hash != new_sky_values_hash)
                {
                    reload_sky_values = true;
                    sky_values_hash = new_sky_values_hash;
                }
            }

            double step_size = 1.0f;
            if (!DA.GetData(10, ref step_size)) { return; }
            step_size = Math.Max(0.0f, step_size);

            int step_count = 1;
            if (!DA.GetData(11, ref step_count)) { return; }
            step_count = Math.Max(0, step_count);

            bool compute_simulation = false;
            if (!DA.GetData(12, ref compute_simulation)) { return; }

            bool _reset_simulation = false;
            if (!DA.GetData(13, ref _reset_simulation)) { return; }

            int new_simulation_step = -1;
            DA.GetData(14, ref new_simulation_step);

            if (new_simulation_step != simulation_step)
            {
                compute_simulation = true;
                simulation_step = new_simulation_step;
            }

            bool _enable_steady_state = false;
            if (!DA.GetData(15, ref _enable_steady_state)) { return; }

            int _vertex_value_type = 0;
            if (!DA.GetData(16, ref _vertex_value_type)) { return; }

            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, String.Format("step_size = {0}, step_count = {1}", step_size, step_count));

            if (goos.Count != meshes.Count)
            {
                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Error, String.Format("Geometry count ({0}) does not match mesh count ({1})", goos.Count, meshes.Count));
                return;
            }

            uint object_count = ((uint)meshes.Count);
            ObjectProperties[] object_properties = new ObjectProperties[object_count];

            for (int i = 0; i < goos.Count; i++)
            {
                IGH_GeometricGoo goo = goos[i];

                // get geometry simulation parameters

                bool refValid = true;
                if (!goo.IsReferencedGeometry)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Geometry is not referenced and therefore doesn't have attributes.");
                    refValid = false;
                }

                Guid id = goo.ReferenceID;
                if (id == Guid.Empty)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Reference ID is blank.");
                    refValid = false;
                }

                ObjRef objRef = new ObjRef(id);
                if (refValid && objRef.Object() == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Referenced object no longer exists in the current document.");
                    refValid = false;
                }
                if (refValid && objRef.Object().Document == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Referenced object is not associated with a document.");
                    refValid = false;
                }

                if (refValid)
                {
                    NameValueCollection attr_usrstr = objRef.Object().Attributes.GetUserStrings();

                    object_properties[i].kelvin = 0.0f;
                    object_properties[i].temperature_fixed = false;

                    object_properties[i].thickness = 1.0f;
                    object_properties[i].density = 1.0f;
                    object_properties[i].heat_capacity = 1.0f;
                    object_properties[i].heat_conductivity = 1.0f;

                    object_properties[i].diffuse_reflectance = 0.5f;
                    object_properties[i].specular_reflectance = 0.0f;

                    object_properties[i].diffuse_emission = true;
                    object_properties[i].traceable = true;

                    Single.TryParse(attr_usrstr.Get("kelvin"), out object_properties[i].kelvin);

                    float temperature_fixed = 0.0f;
                    Single.TryParse(attr_usrstr.Get("temperature-fixed"), out temperature_fixed);
                    object_properties[i].temperature_fixed = temperature_fixed == 1.0f;

                    float diffuse_emission = 1.0f;
                    if (!Single.TryParse(attr_usrstr.Get("diffuse-emission"), out diffuse_emission))
                        diffuse_emission = 1.0f;
                    object_properties[i].diffuse_emission = diffuse_emission == 1.0f;

                    if (!Single.TryParse(attr_usrstr.Get("thickness"), out object_properties[i].thickness))
                        object_properties[i].thickness = 1.0f;
                    if (!Single.TryParse(attr_usrstr.Get("density"), out object_properties[i].density))
                        object_properties[i].density = 1.0f;
                    if (!Single.TryParse(attr_usrstr.Get("heat-capacity"), out object_properties[i].heat_capacity))
                        object_properties[i].heat_capacity = 1.0f;
                    if (!Single.TryParse(attr_usrstr.Get("heat-conductivity"), out object_properties[i].heat_conductivity))
                        object_properties[i].heat_conductivity = 1.0f;
                    if (!Single.TryParse(attr_usrstr.Get("diffuse-reflectance"), out object_properties[i].diffuse_reflectance))
                        object_properties[i].diffuse_reflectance = 0.5f;
                    if (!Single.TryParse(attr_usrstr.Get("specular-reflectance"), out object_properties[i].specular_reflectance))
                        object_properties[i].specular_reflectance = 0.0f;

                    float traceable = 1.0f;
                    if (!Single.TryParse(attr_usrstr.Get("traceable"), out traceable))
                        traceable = 1.0f;
                    object_properties[i].traceable = traceable == 1.0f;
                }
            }

            uint vertex_count = 0;
            uint indices_count = 0;

            for (int i = 0; i < meshes.Count; i++)
            {
                Mesh mesh = meshes[i];

                object_properties[i].vertex_offset = vertex_count;
                object_properties[i].vertex_count = ((uint)mesh.Vertices.Count);

                object_properties[i].indices_offset = indices_count;
                object_properties[i].indices_count = ((uint)mesh.Faces.Count);

                vertex_count += ((uint)mesh.Vertices.Count);
                indices_count += ((uint)mesh.Faces.Count);

                System.Drawing.Color[] vertexColors = new System.Drawing.Color[mesh.Vertices.Count];
                Random randNum = new Random();
                for (int j = 0; j < mesh.Vertices.Count; j++)
                    vertexColors[j] = System.Drawing.Color.FromArgb(randNum.Next());
                mesh.VertexColors.SetColors(vertexColors);
            }

            this.AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, String.Format("total | vertices: {0} | indices: {1}", vertex_count, indices_count));

            if (vertex_colors == null || reload_geometry)
                vertex_colors = new float[vertex_count * 3];

            if (reload_geometry)
            { 
                unload_scene();
                loadGeometry = true;
            }

            if (reload_sky_values)
                loadSkyValues = true;

            if (loadGeometry)
            {
                // GEOMETRY
                {
                    float[] vertices = new float[vertex_count * 3];
                    uint[] indices = new uint[indices_count * 3];

                    uint vi = 0;
                    uint vci = 0;
                    uint ii = 0;
                    foreach (Mesh mesh in meshes)
                    {
                        for (int i = 0; i < mesh.Vertices.Count; i++)
                        {
                            var mv = mesh.Vertices[i];
                            vertices[vi++] = mv.X;
                            vertices[vi++] = mv.Y;
                            vertices[vi++] = mv.Z;

                            var mvc = mesh.VertexColors[i];
                            vertex_colors[vci++] = mvc.R / 255.0f;
                            vertex_colors[vci++] = mvc.G / 255.0f;
                            vertex_colors[vci++] = mvc.B / 255.0f;
                        }

                        for (int i = 0; i < mesh.Faces.Count; i++)
                        {
                            var face = mesh.Faces[i];
                            if (face.C != face.D)
                                this.AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Non triangle encountered!");

                            indices[ii++] = ((uint)face.A);
                            indices[ii++] = ((uint)face.B);
                            indices[ii++] = ((uint)face.C);
                        }
                    }

                    load_geometry(
                        vertices,
                        vertex_colors,
                        vertex_count * 3,
                        indices,
                        indices_count * 3,
                        object_properties,
                        object_count);
                }

                // SKY
                {
                    var sky_vertices = sky_mesh.Vertices;
                    uint sky_vertex_count = (uint)sky_vertices.Count;

                    float[] vertices = new float[sky_vertex_count * 3];

                    uint vc = 0;
                    for (int i = 0; i < sky_vertex_count; i++)
                    {
                        var mv = sky_vertices[i];
                        vertices[vc++] = mv.X;
                        vertices[vc++] = mv.Y;
                        vertices[vc++] = mv.Z;
                    }

                    uint sky_quad_count = (uint)sky_mesh.Faces.Count;
                    uint[] indices = new uint[sky_quad_count * 4];
                    float[] quad_values = new float[sky_quad_count];

                    int fi = 0;
                    int ti = 0;
                    uint ii = 0;
                    foreach (var face in sky_mesh.Faces)
                    {
                        indices[ii++] = ((uint)face.A);
                        indices[ii++] = ((uint)face.B);
                        indices[ii++] = ((uint)face.C);
                        indices[ii++] = ((uint)face.D);

                        quad_values[ti++] = (float)(1000 * sky_mesh_values[fi++].Value / sky_mesh_values_timespan); // convert to from kWh / m^2 to W / m^2
                    }

                    load_sky(
                        vertices,
                        sky_vertex_count,
                        indices,
                        quad_values,
                        sky_quad_count
                        );
                }

                loadGeometry = false;
            }

            if(loadSkyValues)
            {
                uint sky_quad_count = (uint)sky_mesh.Faces.Count;
                float[] quad_values = new float[sky_quad_count];

                int fi = 0;
                int ti = 0;
                foreach (var face in sky_mesh.Faces)
                    quad_values[ti++] = (float)(1000 * sky_mesh_values[fi++].Value / sky_mesh_values_timespan); // convert to from kWh / m^2 to W / m^2

                update_sky_values(quad_values, sky_quad_count);
                loadSkyValues = false;
            }

            if (_reset_simulation)
            {
                time_in_hours = 0;
                reset_simulation();
            }

            if (compute_simulation)
            {
                set_steady_state(_enable_steady_state);

                simulate(
                    (float)step_size,
                    (uint)step_count,
                    ref time_in_hours);
            }

            float[] temperatures = new float[vertex_count];

            {
                if (_vertex_value_type > 0)
                    get_vertex_values(vertex_colors, vertex_count * 3, (uint)_vertex_value_type);
                else
                    get_vertex_temperatures(vertex_colors, vertex_count * 3);
                    
                float max_val = 0;
                for (int i = 0; i < vertex_count * 3; i++)
                {
                    float v = vertex_colors[i];
                    if (max_val < v)
                        max_val = v;
                }

                if (max_val == 0)
                    max_val = 1;

                uint ti = 0;
                uint vci = 0;
                for (int i = 0; i < meshes.Count; i++)
                {
                    Mesh mesh = meshes[i];

                    System.Drawing.Color[] vertexColors = new System.Drawing.Color[mesh.Vertices.Count];
                    for (int j = 0; j < mesh.Vertices.Count; j++)
                    {
                        Grasshopper.GUI.Gradient.GH_Gradient heat = Grasshopper.GUI.Gradient.GH_Gradient.GreyScale();
                        vertexColors[j] = heat.ColourAt(1 - (vertex_colors[vci] / max_val));
                        temperatures[ti++] = vertex_colors[vci];
                        vci += 3;
                    }
                    mesh.VertexColors.SetColors(vertexColors);
                }
            }

            DA.SetDataList(0, meshes);
            DA.SetDataList(1, temperatures);
            DA.SetData(2, time_in_hours);
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("b08c7e6b-dddd-4ec5-877e-54e45ce67579"); }
        }
    }
}
