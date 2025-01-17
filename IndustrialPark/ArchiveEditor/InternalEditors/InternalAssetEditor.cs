﻿using IndustrialPark.Models;
using RenderWareFile;
using SharpDX;
using System;
using System.IO;
using System.Windows.Forms;
using static IndustrialPark.Models.Assimp_IO;
using static IndustrialPark.Models.BSP_IO;
using static IndustrialPark.Models.BSP_IO_Shared;
using HipHopFile;
using System.Collections.Generic;
using System.Linq;

namespace IndustrialPark
{
    public partial class InternalAssetEditor : Form, IInternalEditor
    {
        public InternalAssetEditor(Asset asset, ArchiveEditorFunctions archive, Action<Asset> updateListView)
        {
            InitializeComponent();
            TopMost = true;

            this.asset = asset;
            this.archive = archive;
            this.updateListView = updateListView;

            DynamicTypeDescriptor dt = new DynamicTypeDescriptor(asset.GetType());
            asset.SetDynamicProperties(dt);
            propertyGridAsset.SelectedObject = dt.FromComponent(asset);

            Text = $"[{asset.assetType}] {asset}";

            if (asset is AssetCAM cam) SetupForCam(cam);
            //else if (asset is AssetCSN csn) SetupForCsn(csn);
            else if (asset is IAssetAddSelected aas) SetupForAddSelected(aas);
            else if (asset is AssetRenderWareModel arwm) SetupForModel(arwm);
            else if (asset is AssetSHRP shrp) SetupForShrp(shrp);
            else if (asset is AssetUIM uim) SetupForUim(uim);
            else if (asset is AssetWIRE wire) SetupForWire(wire);

            AddRow(ButtonSize);

            Button buttonHelp = new Button() { Dock = DockStyle.Fill, Text = "Open Wiki Page", AutoSize = true };
            buttonHelp.Click += (object sender, EventArgs e) =>
            {
                var code = asset.assetType.GetCode();
                if (asset.assetType.IsDyna())
                    code += $"/{asset.TypeString}";
                System.Diagnostics.Process.Start(AboutBox.WikiLink + code);
            };
            tableLayoutPanel1.Controls.Add(buttonHelp, 0, tableLayoutPanel1.RowCount - 1);

            Button buttonFindCallers = new Button() { Dock = DockStyle.Fill, Text = "Find Who Targets Me", AutoSize = true };
            buttonFindCallers.Click += (object sender, EventArgs e) =>
                Program.MainForm.FindWhoTargets(GetAssetID());
            tableLayoutPanel1.Controls.Add(buttonFindCallers, 1, tableLayoutPanel1.RowCount - 1);

            for (int i = 1; i < tableLayoutPanel1.RowCount; i++)
            {
                var rs = new RowStyle(SizeType.Absolute, RowSizes[i]);
                if (i < tableLayoutPanel1.RowStyles.Count)
                    tableLayoutPanel1.RowStyles[i] = rs;
                else
                    tableLayoutPanel1.RowStyles.Add(rs);
            }
        }

        private void InternalAssetEditor_FormClosing(object sender, FormClosingEventArgs e)
        {
            archive.CloseInternalEditor(this);
        }

        private readonly Asset asset;
        private readonly ArchiveEditorFunctions archive;
        private readonly Action<Asset> updateListView;

        public uint GetAssetID()
        {
            return asset.assetID;
        }

        public void RefreshPropertyGrid()
        {
            propertyGridAsset.Refresh();
            updateListView(asset);
        }

        private void propertyGridAsset_PropertyValueChanged(object s, PropertyValueChangedEventArgs e)
        {
            archive.UnsavedChanges = true;
            RefreshPropertyGrid();
        }

        private readonly List<int> RowSizes = new List<int>() { -1 };
        private const int ButtonSize = 28;
        private const int CheckboxLabelSize = 22;

        private int AddRow(int size)
        {
            tableLayoutPanel1.RowCount += 1;
            RowSizes.Add(size);
            return tableLayoutPanel1.RowCount - 1;
        }

        private void SetupForCam(AssetCAM asset)
        {
            var rowIndex = AddRow(ButtonSize);

            Button buttonGetPos = new Button() { Dock = DockStyle.Fill, Text = "Get View Position", AutoSize = true };
            buttonGetPos.Click += (object sender, EventArgs e) =>
            {
                asset.SetPosition(Program.MainForm.renderer.Camera.Position);

                RefreshPropertyGrid();
                archive.UnsavedChanges = true;
            };
            tableLayoutPanel1.Controls.Add(buttonGetPos, 0, rowIndex);

            Button buttonGetDir = new Button() { Dock = DockStyle.Fill, Text = "Get View Direction", AutoSize = true };
            buttonGetDir.Click += (object sender, EventArgs e) =>
            {
                asset.SetNormalizedForward(Program.MainForm.renderer.Camera.Forward);
                asset.SetNormalizedUp(Program.MainForm.renderer.Camera.Up);
                asset.SetNormalizedLeft(Program.MainForm.renderer.Camera.Right);

                RefreshPropertyGrid();
                archive.UnsavedChanges = true;
            };
            tableLayoutPanel1.Controls.Add(buttonGetDir, 1, rowIndex);
        }

        private void SetupForAddSelected(IAssetAddSelected asset)
        {
            var rowIndex = AddRow(ButtonSize);

            Button buttonAddSelected = new Button() { Dock = DockStyle.Fill, Text = "Add selected to " + asset.GetItemsText, AutoSize = true };
            buttonAddSelected.Click += (object sender, EventArgs e) =>
            {
                asset.AddItems(archive.GetCurrentlySelectedAssetIDs().ToList());
                RefreshPropertyGrid();
                archive.UnsavedChanges = true;
            };
            tableLayoutPanel1.Controls.Add(buttonAddSelected, 0, rowIndex);
            tableLayoutPanel1.SetColumnSpan(buttonAddSelected, 2);
        }

        //private void SetupForCsn(AssetCSN asset)
        //{
        //    AddRow();

        //    Button buttonExportModlsAnims = new Button() { Dock = DockStyle.Fill, Text = "Export All MODL/ANIM", AutoSize = true };
        //    buttonExportModlsAnims.Click += (object sender, EventArgs e) =>
        //    {
        //        CommonOpenFileDialog saveFile = new CommonOpenFileDialog()
        //        {
        //            IsFolderPicker = true,
        //        };

        //        if (saveFile.ShowDialog() == CommonFileDialogResult.Ok)
        //            asset.ExtractToFolder(saveFile.FileName);
        //    };
        //    tableLayoutPanel1.Controls.Add(buttonExportModlsAnims);
        //    tableLayoutPanel1.SetColumnSpan(buttonExportModlsAnims, 2);
        //}

        private void SetupForModel(AssetRenderWareModel asset)
        {
            var rowIndex = AddRow(ButtonSize);

            Button buttonApplyVertexColors = new Button() { Dock = DockStyle.Fill, Text = "Apply Vertex Colors", AutoSize = true };
            buttonApplyVertexColors.Click += (object sender, EventArgs e) =>
            {
                var (color, operation) = ApplyVertexColors.GetColor();
                if (color.HasValue)
                {
                    var performOperation = new Func<Vector4, Vector4>((Vector4 oldColor) => new Vector4(
                        operation.PerformAndClamp(oldColor.X, color.Value.X),
                        operation.PerformAndClamp(oldColor.Y, color.Value.Y),
                        operation.PerformAndClamp(oldColor.Z, color.Value.Z),
                        operation.PerformAndClamp(oldColor.W, color.Value.W)));

                    asset.ApplyVertexColors(performOperation);
                    archive.UnsavedChanges = true;
                }
            };
            tableLayoutPanel1.Controls.Add(buttonApplyVertexColors, 0, rowIndex);

            Button buttonApplyScale = new Button() { Dock = DockStyle.Fill, Text = "Apply Scale", AutoSize = true };
            buttonApplyScale.Click += (object sender, EventArgs e) =>
            {
                var factor = ApplyScale.GetVector();
                if (factor.HasValue)
                {
                    asset.ApplyScale(factor.Value);
                    archive.UnsavedChanges = true;
                }
            };
            tableLayoutPanel1.Controls.Add(buttonApplyScale, 1, rowIndex);

            rowIndex = AddRow(CheckboxLabelSize);

            Label importSettings = new Label() { Dock = DockStyle.Fill, Text = "Import settings:", AutoSize = true };
            tableLayoutPanel1.Controls.Add(importSettings, 0, rowIndex);

            Label exportSettings = new Label() { Dock = DockStyle.Fill, Text = "Export settings:", AutoSize = true };
            tableLayoutPanel1.Controls.Add(exportSettings, 1, rowIndex);

            rowIndex = AddRow(CheckboxLabelSize);

            CheckBox flipUVs = new CheckBox() { Dock = DockStyle.Fill, Text = "Flip UVs", AutoSize = true };
            tableLayoutPanel1.Controls.Add(flipUVs, 0, rowIndex);

            CheckBox exportTextures = new CheckBox() { Dock = DockStyle.Fill, Text = "Export Textures", AutoSize = true };
            tableLayoutPanel1.Controls.Add(exportTextures, 1, rowIndex);

            rowIndex = AddRow(CheckboxLabelSize);

            CheckBox ignoreMeshColors = new CheckBox() { Dock = DockStyle.Fill, Text = "Ignore Mesh Colors", AutoSize = true, Checked = true };
            tableLayoutPanel1.Controls.Add(ignoreMeshColors, 0, rowIndex);

            rowIndex = AddRow(ButtonSize);

            Button buttonImport = new Button() { Dock = DockStyle.Fill, Text = "Import", AutoSize = true };
            buttonImport.Click += (object sender, EventArgs e) =>
            {
                OpenFileDialog openFile = new OpenFileDialog()
                {
                    Filter = GetImportFilter(), // "All supported types|*.dae;*.obj;*.bsp|DAE Files|*.dae|OBJ Files|*.obj|BSP Files|*.bsp|All files|*.*",
                };

                if (openFile.ShowDialog() == DialogResult.OK)
                {
                    if (asset.assetType == AssetType.Model)
                        asset.Data = Path.GetExtension(openFile.FileName).ToLower().Equals(".dff") ?
                        File.ReadAllBytes(openFile.FileName) :
                        ReadFileMethods.ExportRenderWareFile(CreateDFFFromAssimp(openFile.FileName, flipUVs.Checked, ignoreMeshColors.Checked), modelRenderWareVersion(asset.game));

                    if (asset.assetType == AssetType.BSP)
                        asset.Data = Path.GetExtension(openFile.FileName).ToLower().Equals(".bsp") ?
                        File.ReadAllBytes(openFile.FileName) :
                        ReadFileMethods.ExportRenderWareFile(CreateBSPFromAssimp(openFile.FileName, flipUVs.Checked, ignoreMeshColors.Checked), modelRenderWareVersion(asset.game));

                    asset.Setup(Program.MainForm.renderer);

                    archive.UnsavedChanges = true;
                }
            };
            tableLayoutPanel1.Controls.Add(buttonImport, 0, rowIndex);

            Button buttonExport = new Button() { Dock = DockStyle.Fill, Text = "Export", AutoSize = true };
            buttonExport.Click += (object sender, EventArgs e) =>
            {
                (Assimp.ExportFormatDescription format, string textureExtension) = ChooseTarget.GetTarget();

                if (format != null)
                {
                    SaveFileDialog a = new SaveFileDialog()
                    {
                        Filter = format == null ? "RenderWare BSP|*.bsp" : format.Description + "|*." + format.FileExtension,
                    };

                    if (a.ShowDialog() == DialogResult.OK)
                    {
                        if (format == null)
                            File.WriteAllBytes(a.FileName, asset.Data);
                        else if (format.FileExtension.ToLower().Equals("obj") && asset.assetType == AssetType.BSP)
                            ConvertBSPtoOBJ(a.FileName, ReadFileMethods.ReadRenderWareFile(asset.Data), true);
                        else
                            ExportAssimp(Path.ChangeExtension(a.FileName, format.FileExtension), ReadFileMethods.ReadRenderWareFile(asset.Data), true, format, textureExtension, Matrix.Identity);

                        if (exportTextures.Checked)
                        {
                            string folderName = Path.GetDirectoryName(a.FileName);
                            var bitmaps = archive.GetTexturesAsBitmaps(asset.Textures);
                            ReadFileMethods.treatStuffAsByteArray = false;
                            foreach (string textureName in bitmaps.Keys)
                                bitmaps[textureName].Save(folderName + "/" + textureName + ".png", System.Drawing.Imaging.ImageFormat.Png);
                        }
                    }
                }
            };

            tableLayoutPanel1.Controls.Add(buttonExport, 1, rowIndex);
        }

        private void SetupForShrp(AssetSHRP asset)
        {
            AddRow(ButtonSize);
            AddRow(ButtonSize);

            var validTypes = new List<IShrapnelType>()
            {
                IShrapnelType.Particle,
                IShrapnelType.Projectile,
                IShrapnelType.Lightning,
                IShrapnelType.Sound
            };

            if (asset.game == Game.Incredibles)
            {
                AddRow(ButtonSize);
                AddRow(ButtonSize);

                validTypes.AddRange(new IShrapnelType[] {
                     IShrapnelType.Shockwave,
                     IShrapnelType.Explosion,
                     IShrapnelType.Distortion,
                     IShrapnelType.Fire,
                });
            }

            foreach (var i in validTypes)
            {
                Button buttonAdd = new Button() { Dock = DockStyle.Fill, Text = $"Add {i}", AutoSize = true };
                buttonAdd.Click += (object sender, EventArgs e) =>
                {
                    asset.AddEntry(i);
                    RefreshPropertyGrid();
                    archive.UnsavedChanges = true;
                };
                tableLayoutPanel1.Controls.Add(buttonAdd);
            }
        }

        private void SetupForUim(AssetUIM asset)
        {
            AddRow(ButtonSize);
            AddRow(ButtonSize);
            AddRow(ButtonSize);
            AddRow(ButtonSize);

            foreach (UIMCommandType uimct in Enum.GetValues(typeof(UIMCommandType)))
            {
                Button buttonAdd = new Button() { Dock = DockStyle.Fill, Text = $"Add command: {uimct}", AutoSize = true };
                buttonAdd.Click += (object sender, EventArgs e) =>
                {
                    asset.AddEntry(uimct);
                    RefreshPropertyGrid();
                    archive.UnsavedChanges = true;
                };
                tableLayoutPanel1.Controls.Add(buttonAdd);
            }
        }

        private void SetupForWire(AssetWIRE asset)
        {
            var rowIndex = AddRow(ButtonSize);

            Button buttonImport = new Button() { Dock = DockStyle.Fill, Text = "Import", AutoSize = true };
            buttonImport.Click += (object sender, EventArgs e) =>
            {
                OpenFileDialog openFile = new OpenFileDialog
                {
                    Filter = "OBJ Files|*.obj|All files|*.*",
                };

                if (openFile.ShowDialog() == DialogResult.OK)
                {
                    asset.FromObj(openFile.FileName);
                    archive.UnsavedChanges = true;
                }
            };
            tableLayoutPanel1.Controls.Add(buttonImport, 0, rowIndex);

            Button buttonExport = new Button() { Dock = DockStyle.Fill, Text = "Export", AutoSize = true };
            buttonExport.Click += (object sender, EventArgs e) =>
            {
                SaveFileDialog saveFile = new SaveFileDialog()
                {
                    Filter = "OBJ Files|*.obj|All files|*.*",
                };

                if (saveFile.ShowDialog() == DialogResult.OK)
                    asset.ToObj(saveFile.FileName);
            };
            tableLayoutPanel1.Controls.Add(buttonExport, 1, rowIndex);
        }
    }
}
