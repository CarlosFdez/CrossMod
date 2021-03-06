﻿using CrossMod.GUI;
using CrossMod.Nodes;
using System;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CrossMod.IO;

namespace CrossMod
{
    public partial class MainForm : Form
    {
        // Controls
        private ModelViewport modelViewport;

        private ContextMenu fileTreeContextMenu;

        public MainForm()
        {
            InitializeComponent();

            modelViewport = new ModelViewport
            {
                Dock = DockStyle.Fill
            };

            fileTreeContextMenu = new ContextMenu();
        }

        public void HideControl()
        {
            contentBox.Controls.Clear();
            modelViewport.Clear();
        }

        public void ShowModelViewport()
        {
            HideControl();
            contentBox.Controls.Add(modelViewport);
        }

        private void openModelFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string folderPath = FileTools.TryOpenFolder();
            if (string.IsNullOrEmpty(folderPath))
                return;

            OpenFiles(folderPath);

            ShowModelViewport();

            // Render models if present.
            SelectFirstNode("numdlb");
        }

        private void OpenFiles(string folderPath)
        {
            string[] files = Directory.GetFiles(folderPath);

            var Types = (from domainAssembly in AppDomain.CurrentDomain.GetAssemblies()
                         from assemblyType in domainAssembly.GetTypes()
                         where typeof(FileNode).IsAssignableFrom(assemblyType)
                         select assemblyType).ToArray();

            TreeNode Parent = new TreeNode(Path.GetDirectoryName(folderPath));
            fileTree.Nodes.Add(Parent);

            foreach (string file in files)
            {
                OpenFile(Types, Parent, file);
            }
        }

        private static void OpenFile(Type[] Types, TreeNode Parent, string file)
        {
            FileNode Node = null;

            string Extension = Path.GetExtension(file);

            foreach (Type type in Types)
            {
                if (type.GetCustomAttributes(typeof(FileTypeAttribute), true).FirstOrDefault() is FileTypeAttribute attr)
                {
                    if (attr.Extension.Equals(Extension))
                    {
                        Node = (FileNode)Activator.CreateInstance(type);
                    }
                }
            }

            if (Node == null)
                Node = new FileNode();

            Node.Open(file);

            Node.Text = Path.GetFileName(file);
            Parent.Nodes.Add(Node);
        }

        private void fileTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (fileTree.SelectedNode is IRenderableNode renderableNode)
            {
                if (renderableNode != null)
                {
                    ShowModelViewport();
                    modelViewport.RenderableNode = renderableNode;
                }
            }

            modelViewport.RenderFrame();
        }

        private void reloadShadersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Force the shader to be generated again.
            Rendering.ShaderContainer.SetUpShaders();
        }

        private void renderSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var settingsMenu = new RenderSettingsMenu();
            settingsMenu.Show();
        }

        private void fileTree_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                // Point where the mouse is clicked.
                Point p = new Point(e.X, e.Y);

                // Get the node that the user has clicked.
                TreeNode node = fileTree.GetNodeAt(p);
                if (node != null)
                {
                    fileTree.SelectedNode = node;
                    fileTreeContextMenu.MenuItems.Clear();

                    // gather all options for this node
                    if (node is IExportableModelNode exportableNode)
                    {
                        MenuItem ExportSMD = new MenuItem("Export As");
                        ExportSMD.Click += exportExportableModelAsSMD;
                        ExportSMD.Tag = exportableNode;
                        fileTreeContextMenu.MenuItems.Add(ExportSMD);
                    }
                    if (node is IExportableAnimationNode exportableAnimNode)
                    {
                        MenuItem ExportAnim = new MenuItem("Export Anim");
                        ExportAnim.Click += exportExportableAnimation;
                        ExportAnim.Tag = exportableAnimNode;
                        fileTreeContextMenu.MenuItems.Add(ExportAnim);
                    }

                    // show if it has at least 1 option
                    if (fileTreeContextMenu.MenuItems.Count != 0)
                        fileTreeContextMenu.Show(fileTree, p);
                }
            }
        }

        private void exportExportableAnimation(object sender, EventArgs args)
        {
            if (FileTools.TrySaveFile(out string fileName, "Supported Files(*.smd, *.seanim, *.anim)|*.smd;*.seanim;*.anim"))
            {
                // need to get RSkeleton First for some types
                if (fileName.EndsWith(".smd") || fileName.EndsWith(".anim"))
                {
                    Rendering.RSkeleton SkeletonNode = null;
                    if (FileTools.TryOpenFile(out string SkeletonFileName, "SKEL (*.nusktb)|*.nusktb"))
                    {
                        if (SkeletonFileName != null)
                        {
                            SKEL_Node node = new SKEL_Node();
                            node.Open(SkeletonFileName);
                            SkeletonNode = (Rendering.RSkeleton)node.GetRenderableNode();
                        }
                    }
                    if (SkeletonNode == null)
                    {
                        MessageBox.Show("No Skeleton File Selected");
                        return;
                    }

                    if (fileName.EndsWith(".anim"))
                    {
                        bool Ordinal = false;
                        DialogResult dialogResult = MessageBox.Show("In most cases choose \"No\"", "Use ordinal bone order?", MessageBoxButtons.YesNo);
                        if (dialogResult == DialogResult.Yes)
                            Ordinal = true;
                        IO_MayaANIM.ExportIOAnimationAsANIM(fileName, ((IExportableAnimationNode)((MenuItem)sender).Tag).GetIOAnimation(), SkeletonNode, Ordinal);
                    }

                    if (fileName.EndsWith(".smd"))
                        IO_SMD.ExportIOAnimationAsSMD(fileName, ((IExportableAnimationNode)((MenuItem)sender).Tag).GetIOAnimation(), SkeletonNode);
                }

                // other types like SEAnim go here
                if (fileName.EndsWith(".seanim"))
                {
                    IO_SEANIM.ExportIOAnimation(fileName, ((IExportableAnimationNode)((MenuItem)sender).Tag).GetIOAnimation());
                }
            }
        }

        private void exportExportableModelAsSMD(object sender, EventArgs args)
        {
            if (FileTools.TrySaveFile(out string fileName, "Supported Files(*.smd*.obj*.dae*.ply)|*.smd;*.obj;*.dae;*.ply"))
            {
                if (fileName.EndsWith(".smd"))
                    IO_SMD.ExportIOModelAsSMD(fileName, ((IExportableModelNode)((MenuItem)sender).Tag).GetIOModel());
                if (fileName.EndsWith(".obj"))
                    IO_OBJ.ExportIOModelAsOBJ(fileName, ((IExportableModelNode)((MenuItem)sender).Tag).GetIOModel());
                if (fileName.EndsWith(".dae"))
                    IO_DAE.ExportIOModelAsDAE(fileName, ((IExportableModelNode)((MenuItem)sender).Tag).GetIOModel());
                if (fileName.EndsWith(".ply"))
                    IO_PLY.ExportIOModelAsPLY(fileName, ((IExportableModelNode)((MenuItem)sender).Tag).GetIOModel());
            }
        }

        private void clearWorkspaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ClearWorkspace();
        }

        private void ClearWorkspace()
        {
            fileTree.Nodes.Clear();
            modelViewport.ClearFiles();
            HideControl();
            GC.Collect();
        }

        private void batchRenderModelsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            BatchRenderModels();
        }

        private void BatchRenderModels()
        {
            string folderPath = FileTools.TryOpenFolder("Select Source Directory");
            if (string.IsNullOrEmpty(folderPath))
                return;

            string outputPath = FileTools.TryOpenFolder("Select PNG Output Directory");
            if (string.IsNullOrEmpty(outputPath))
                return;

            foreach (var file in Directory.EnumerateFiles(folderPath, "*model.numdlb", SearchOption.AllDirectories))
            {
                // Just render the first alt costume, which will include models without slot specific variants.
                //if (!file.Contains("c00"))
                //    continue;

                string sourceFolder = Directory.GetParent(file).FullName;

                OpenFiles(sourceFolder);

                ShowModelViewport();

                // Necessary workaround for how models are displayed.
                SelectFirstNode("numdlb");

                modelViewport.RenderFrame();

                // Save screenshot.
                using (var bmp = modelViewport.GetScreenshot())
                {
                    string condensedName = GetCondensedPathName(folderPath, file);
                    bmp.Save(Path.Combine(outputPath, $"{condensedName}.png"));
                }

                // Cleanup.
                ClearWorkspace();
                System.Diagnostics.Debug.WriteLine($"Rendered {sourceFolder}");
            }
        }

        private static string GetCondensedPathName(string folderPath, string file)
        {
            string condensedName = file.Replace(folderPath, "");
            condensedName = condensedName.Replace(Path.DirectorySeparatorChar, '_');
            condensedName = condensedName.Substring(1); // remove leading underscore
            return condensedName;
        }

        private void SelectFirstNode(string endingText)
        {
            foreach (TreeNode folderNode in fileTree.Nodes)
            {
                foreach (TreeNode fileNode in folderNode.Nodes)
                {
                    if (fileNode.Text.EndsWith(endingText))
                    {
                        fileTree.SelectedNode = fileNode;
                        return;
                    }
                }
            }
        }

        private void frameSelectionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (fileTree.SelectedNode is NUMDL_Node node)
            {
                var rnumdl = node.GetRenderableNode() as Rendering.RNUMDL;
                modelViewport.FrameSelection(rnumdl.Model);
            }
        }

        private void printMaterialValuesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string folderPath = FileTools.TryOpenFolder("Select Source Directory");
            if (string.IsNullOrEmpty(folderPath))
                return;

            uint paramId = Rendering.RenderSettings.Instance.ParamId;

            var values = new System.Collections.Generic.HashSet<string>();

            var outputText = new System.Text.StringBuilder();

            foreach (var file in Directory.EnumerateFiles(folderPath, "*numatb", SearchOption.AllDirectories))
            {
                var matl = new MATL_Node();
                matl.Open(file);

                foreach (var entry in matl.Material.Entries)
                {
                    foreach (var attribute in entry.Attributes)
                    {
                        if ((uint)attribute.ParamID == paramId)
                        {
                            string text = $"{paramId.ToString("X")} {attribute.DataObject} {file.Replace(folderPath, "")}";
                            if (!values.Contains(attribute.DataObject.ToString()))
                            {
                                outputText.AppendLine(text);
                                values.Add(attribute.DataObject.ToString());
                            }
                        }
                    }
                }
            }

            File.WriteAllText("output.txt", outputText.ToString());
        }
    }
}
