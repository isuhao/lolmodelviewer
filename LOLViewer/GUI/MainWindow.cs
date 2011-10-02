﻿
/*
LOLViewer
Copyright 2011 James Lammlein 

 

This file is part of LOLViewer.

LOLViewer is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
any later version.

LOLViewer is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with LOLViewer.  If not, see <http://www.gnu.org/licenses/>.

*/


using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using OpenTK;
using LOLViewer.IO;

namespace LOLViewer
{
    public partial class MainWindow : Form
    {
        // Windowing variables
        private bool isGLLoaded;
        private Stopwatch timer;

        // Graphics abstraction
        private GLRenderer renderer;

        // Default Camera
        private const float FIELD_OF_VIEW = OpenTK.MathHelper.PiOver4;
        private const float NEAR_PLANE = 0.1f;
        private const float FAR_PLANE = 1000.0f;
        private GLCamera camera;

        // IO Variables
        LOLDirectoryReader reader;

        // GUI Variables
        // converts from World Transform scale to trackbar units.
        private const float DEFAULT_SCALE_TRACKBAR = 1000.0f;

        public MainWindow()
        {
            isGLLoaded = false;
            timer = new Stopwatch();

            camera = new GLCamera();
            camera.SetViewParameters(new Vector3(0.0f, 0.0f, 100.0f), Vector3.Zero);
            renderer = new GLRenderer();

            reader = new LOLDirectoryReader();

            InitializeComponent();
            modelScaleTrackbar.Value = (int) (GLRenderer.DEFAULT_MODEL_SCALE * DEFAULT_SCALE_TRACKBAR);

            // GLControl Callbacks
            glControlMain.Load += new EventHandler(GLControlMainOnLoad);
            glControlMain.Resize += new EventHandler(GLControlMainOnResize);
            glControlMain.Paint += new PaintEventHandler(GLControlMainOnPaint);
            glControlMain.Disposed += new EventHandler(GLControlMainOnDispose);

            // Set mouse events
            glControlMain.MouseDown += new MouseEventHandler(GLControlOnMouseDown);
            glControlMain.MouseUp += new MouseEventHandler(GLControlOnMouseUp);
            glControlMain.MouseWheel += new MouseEventHandler(GLControlOnMouseWheel);
            glControlMain.MouseMove += new MouseEventHandler(GLControlOnMouseMove);

            // Set keyboard events
            glControlMain.KeyDown += new KeyEventHandler(GLControlMainOnKeyDown);
            glControlMain.KeyUp += new KeyEventHandler(GLControlMainOnKeyUp);

            // Menu Callbacks
            closeToolStripMenuItem.Click += new EventHandler(OnClose);
            setDirectoryToolStripMenuItem.Click += new EventHandler(OnSetDirectory);
            aboutToolStripMenuItem.Click += new EventHandler(OnAbout);
            readToolStripMenuItem.Click += new EventHandler(OnReadModels);

            // Model View Callbacks
            modelListBox.DoubleClick += new EventHandler(OnModelListDoubleClick);
            modelListBox.KeyPress += new KeyPressEventHandler(OnModelListKeyPress);

            // Trackbars
            yOffsetTrackbar.Scroll += new EventHandler(YOffsetTrackbarOnScroll);
            modelScaleTrackbar.Scroll += new EventHandler(ModelScaleTrackbarOnScroll);
        }

        public void GLControlMainOnPaint(object sender, PaintEventArgs e)
        {
            if (isGLLoaded == false)
                return;

            renderer.OnRender(camera);

            glControlMain.SwapBuffers();
        }

        public void GLControlMainOnResize(object sender, EventArgs e)
        {
            if (isGLLoaded == false)
                return;

            // Set up camera projection parameters based on window's size.
            camera.SetProjectionParameters(FIELD_OF_VIEW, (float)(glControlMain.ClientRectangle.Width - glControlMain.ClientRectangle.X),
                (float)(glControlMain.ClientRectangle.Height - glControlMain.ClientRectangle.Y),
                NEAR_PLANE, FAR_PLANE);

            renderer.OnResize(glControlMain.ClientRectangle.X, glControlMain.ClientRectangle.Y,
                glControlMain.ClientRectangle.Width, glControlMain.ClientRectangle.Height);

            GLControlMainOnUpdateFrame(sender, e);
        }

        public void GLControlMainOnLoad(object sender, EventArgs e)
        {
            isGLLoaded = true;

            // Read model files.
            OnReadModels(sender, e);

            // Set up renderer.
            bool result = renderer.OnLoad();
            if (result == false)
            {
                this.Close();
                return;
            }

            // Call an initial resize to get some camera and renderer parameters set up.
            GLControlMainOnResize(sender, e);
            timer.Start();
        }

        public void GLControlMainOnUpdateFrame(object sender, EventArgs e)
        {
            double elapsedTime = ComputeElapsedTime();
            camera.OnUpdate((float) elapsedTime);

            glControlMain.Invalidate();
        }

        void GLControlMainOnDispose(object sender, EventArgs e)
        {
            renderer.ShutDown();
        }

        //
        // Mouse Handlers
        //

        private void GLControlOnMouseMove(object sender, MouseEventArgs e)
        {
            camera.OnMouseMove(e);
            GLControlMainOnUpdateFrame(sender, e);
        }

        private void GLControlOnMouseWheel(object sender, MouseEventArgs e)
        {
            camera.OnMouseWheel(e);
            GLControlMainOnUpdateFrame(sender, e);
        }

        private void GLControlOnMouseUp(object sender, MouseEventArgs e)
        {
            camera.OnMouseButtonUp(e);
            GLControlMainOnUpdateFrame(sender, e);
        }

        private void GLControlOnMouseDown(object sender, MouseEventArgs e)
        {
            camera.OnMouseButtonDown(e);
            GLControlMainOnUpdateFrame(sender, e);
        }

        //
        // Keyboard Handlers
        //

        void GLControlMainOnKeyUp(object sender, KeyEventArgs e)
        {
            camera.OnKeyUp(e);
            GLControlMainOnUpdateFrame(sender, e);
        }

        void GLControlMainOnKeyDown(object sender, KeyEventArgs e)
        {
            camera.OnKeyDown(e);
            GLControlMainOnUpdateFrame(sender, e);

            if (e.KeyCode == Keys.Escape)
            {
                this.Close();
                return;
            }
        }

        //
        // Menu Strip Handlers
        //

        void OnAbout(object sender, EventArgs e)
        {
            // TODO Make a small about form to display.
            // This will do for now.
            MessageBox.Show("LOLViewer 1.0", "About",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void OnSetDirectory(object sender, EventArgs e)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.Description = "Select the 'Riot Games' folder.";
            dlg.ShowNewFolderButton = false;

            DialogResult result = dlg.ShowDialog();
            
            String selectedDir = String.Empty;
            if (result == DialogResult.OK)
            {
                // Check end of string equals 'Riot Games'
                try
                {
                    char[] folder = new char[10];
                    dlg.SelectedPath.CopyTo(dlg.SelectedPath.Length - 10, folder,
                        0, 10);
                    selectedDir = new String(folder);
                }
                catch {}

                if (selectedDir == "Riot Games")
                {
                    // Set the new root.
                    reader.SetRoot(dlg.SelectedPath);
                    
                    // Reread the models.
                    OnReadModels(sender, e);
                }
                else
                {
                    // Output an error
                    MessageBox.Show("The 'Riot Games' folder was not selected.", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        void OnClose(object sender, EventArgs e)
        {
            this.Close();
        }

        void OnReadModels(object sender, EventArgs e)
        {
            bool result = reader.Read();
            if (result == false)
            {
                MessageBox.Show("Unable to read models. If you installed League of legends" +
                                 " in a non-default location, change the default directory" +
                                 " to the 'Riot Games' folder by using the command in the 'Options' menu.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Populate the model list box.
            modelListBox.Items.Clear();
            List<String> modelNames = reader.GetModelNames();
            foreach (String name in modelNames)
            {
                modelListBox.Items.Add(name);
            }
        }

        //
        // Model List Box Handlers
        //

        void OnModelListDoubleClick(object sender, EventArgs e)
        {
            String modelName = (String) modelListBox.SelectedItem;

            // TODO: Not really sure how to handle errors
            // if either of these functions fail.
            LOLModel model = reader.GetModel(modelName);
            if (model != null)
            {
                bool result = renderer.LoadModel(model);
            }

            GLControlMainOnUpdateFrame(sender, e);
        }

        void OnModelListKeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e)
        {
            // When enter is pressed
            if (e.KeyChar == '\r')
            {
                // Update model.
                OnModelListDoubleClick(sender, e);
            }
        }

        //
        // Trackbar Handlers
        //
        void YOffsetTrackbarOnScroll(object sender, EventArgs e)
        {
            Matrix4 world = Matrix4.CreateTranslation(0.0f, (float)-yOffsetTrackbar.Value, 0.0f);
            world *= Matrix4.Scale(modelScaleTrackbar.Value / DEFAULT_SCALE_TRACKBAR);
            renderer.world = world;

            // Redraw.
            GLControlMainOnPaint(sender, null);
        }

        void ModelScaleTrackbarOnScroll(object sender, EventArgs e)
        {
            Matrix4 world = Matrix4.CreateTranslation(0.0f, (float)-yOffsetTrackbar.Value, 0.0f);
            world *= Matrix4.Scale(modelScaleTrackbar.Value / DEFAULT_SCALE_TRACKBAR);
            renderer.world = world;

            // Redraw.
            GLControlMainOnPaint(sender, null);
        }
        
        //
        // Helper Functions
        //

        public double ComputeElapsedTime()
        {
            timer.Stop();
            double elapsedTime = timer.Elapsed.TotalSeconds;
            timer.Reset();
            timer.Start();
            return elapsedTime;
        }
    }
}