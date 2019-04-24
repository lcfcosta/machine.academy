﻿using CLMath;
using Mademy;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NumberRecognize
{
    public partial class Form1 : Form
    {
        private MathLib mathLib = null;
        private Network network = null;
        Bitmap bitmap;
        private Network.TrainingPromise trainingPromise = null;
        Timer trainingtimer = new Timer();
        Form2 progressDialog = null;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            mathLib = new MathLib(null);

            bitmap = new Bitmap(28, 28, System.Drawing.Imaging.PixelFormat.Format16bppRgb565);
            ClearBitmap();
            pictureBox1.Image = bitmap;

            comboBox1.Items.Add("Use CPU calculation");
            comboBox1.SelectedIndex = 0;
            foreach (var device in ComputeDevice.GetDevices())
            {
                string item = "[" + device.GetPlatformID() + ":" + device.GetDeviceID() + ", " + device.GetDeviceType().ToString() + "] " + device.GetName();
                comboBox1.Items.Add(item);
            }

            trainingtimer.Interval = 300;
            trainingtimer.Tick += Trainingtimer_Tick;

            InitRandomNetwork();

        }

        private void Trainingtimer_Tick(object sender, EventArgs e)
        {
            if (progressDialog != null && trainingPromise != null)
            {
                progressDialog.UpdateResult(trainingPromise.GetTotalProgress(), trainingPromise.IsReady(), "Training... Epochs done: " + trainingPromise.GetEpochsDone());
                if (trainingPromise.IsReady())
                {
                    trainingPromise = null;
                    progressDialog = null;
                    trainingtimer.Stop();
                }
            }
        }

        private void InitRandomNetwork()
        {
            List<int> layerConfig = new List<int>();
            layerConfig.Add(bitmap.Size.Width* bitmap.Size.Height);
            layerConfig.Add(128);
            layerConfig.Add(128);
            layerConfig.Add(128);
            layerConfig.Add(10);

            network = Network.CreateNetworkInitRandom(layerConfig);
            network.AttachName("MNIST learning DNN");
            network.AttachDescription("MNIST learning DNN using " + layerConfig.Count + " layers in structure: (" + string.Join(", ", layerConfig) + " )" );
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox1.SelectedIndex == 0)
                mathLib = new MathLib();
            else
                mathLib = new MathLib(ComputeDevice.GetDevices()[comboBox1.SelectedIndex - 1]);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            InitRandomNetwork();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            if (network != null)
            {
                saveFileDialog1.Filter = "JSON File|*.json";
                saveFileDialog1.Title = "Save training data";
                if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                {
                    System.IO.File.WriteAllText(saveFileDialog1.FileName, network.GetTrainingDataJSON());
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            openFileDialog1.Filter = "JSON File|*.json";
            openFileDialog1.Title = "Save training data";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string file = System.IO.File.ReadAllText(openFileDialog1.FileName);
                    var newNetwork = Network.LoadTrainingDataFromJSON(file);
                    network = newNetwork;
                }
                catch (Exception exc)
                {
                    MessageBox.Show("Error when loading network: " + exc.ToString(), "Error",MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void LoadTestDataFromFiles(List<TrainingSuite.TrainingData> trainingData, String labelFileName, String imgFileName)
        {
            var labelData = System.IO.File.ReadAllBytes(labelFileName);
            int labelDataOffset = 8; //first 2x32 bits are not interesting for us.

            var imageData = System.IO.File.ReadAllBytes(imgFileName);
            int imageDataOffset = 16; //first 4x32 bits are not interesting for us.
            int imageSize = bitmap.Size.Width * bitmap.Size.Height;

            for (int i = labelDataOffset; i < labelData.Length; i++)
            {
                int trainingSampleId = i - labelDataOffset;
                int label = labelData[i];
                float[] input = new float[imageSize];
                float[] output = new float[10];
                for (int j = 0; j < bitmap.Size.Height; j++)
                {
                    for (int k = 0; k < bitmap.Size.Width; k++)
                    {
                        int offsetInImage = j * bitmap.Size.Width + k;
                        byte pixelColor = imageData[imageDataOffset + trainingSampleId * imageSize + offsetInImage];
                        input[offsetInImage] = ((float)pixelColor) / 255.0f;
                        //bitmap.SetPixel(k, j, Color.FromArgb(255, 255- pixelColor, 255 - pixelColor, 255 - pixelColor));
                    }
                }
                /*
                pictureBox1.Refresh();
                System.Threading.Thread.Sleep(100);*/
                output[label] = 1.0f;
                trainingData.Add(new TrainingSuite.TrainingData(input, output));
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            string imgFile = "";
            string labelFile = "";

            openFileDialog1.Filter = "Image Training data (Image)|*.*";
            openFileDialog1.Title = "Open Training images file";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                imgFile = openFileDialog1.FileName;
            }
            else
            {
                return;
            }

            openFileDialog1.Filter = "Training data (Label)|*.*";
            openFileDialog1.Title = "Open Training labels file";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                labelFile = openFileDialog1.FileName;
            }
            else
            {
                return;
            }

            List<TrainingSuite.TrainingData> trainingData = new List<TrainingSuite.TrainingData>();

            LoadTestDataFromFiles(trainingData, labelFile, imgFile);

            var trainingSuite = new TrainingSuite(trainingData);
            trainingSuite.config.miniBatchSize = 100;
            trainingSuite.config.numThreads = 1;
            trainingSuite.config.learningRate = 0.015f;
            trainingSuite.config.epochs = (int)numericUpDown1.Value;

            trainingPromise = network.Train(mathLib, trainingSuite);
            trainingtimer.Start();

            progressDialog = new Form2(trainingPromise);
            progressDialog.ShowDialog();
        }

        private void ClearBitmap()
        {
            for (int i = 0; i < bitmap.Size.Height; i++)
            {
                for (int j = 0; j < bitmap.Size.Width; j++)
                {
                    bitmap.SetPixel(j, i, Color.White);
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            ClearBitmap();
            pictureBox1.Refresh();
        }

        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            paintPixel(e);
        }

        private void pictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
        }

        private void paintPixel(MouseEventArgs e)
        {
            float relativePosX = (float)e.X / (float)pictureBox1.Width;
            float relativePosY = (float)e.Y / (float)pictureBox1.Height;

            int bitmapX = (int)Math.Max(0, Math.Min(Math.Floor(relativePosX * (float)(bitmap.Size.Width - 1)), bitmap.Size.Width - 1));
            int bitmapY = (int)Math.Max(0, Math.Min(Math.Floor(relativePosY * (float)(bitmap.Size.Height - 1)), bitmap.Size.Height - 1));
            bitmap.SetPixel(bitmapX, bitmapY, Color.Black);
            pictureBox1.Refresh();
        }

        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                paintPixel(e);
            }
        }

        private void pictureBox1_MouseLeave(object sender, EventArgs e)
        {
        }

        private int ClassifyOutput(float[] output)
        {
            float largest = -1;
            int resultIdx = -1;
            for (int i = 0; i < output.Length; i++)
            {
                if (output[i] > largest)
                {
                    largest = output[i];
                    resultIdx = i;
                }
            }
            return resultIdx;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            float[] input = new float[bitmap.Size.Width * bitmap.Size.Height];

            for (int i = 0; i < bitmap.Size.Height; i++)
            {
                for (int j = 0; j < bitmap.Size.Width; j++)
                {
                    var color = bitmap.GetPixel(j,i).ToArgb();
                    input[i * bitmap.Size.Width + j] = color == -1 ? 0.0f : 1.0f;
                }
            }

            var output = network.Compute(mathLib, input);

            int resultIdx = ClassifyOutput(output);

            lblResult.Text = "Results:\nI think you drew a " + resultIdx + "\nOutput was:\n";
            lblResult.Text += string.Join("\n ", output);
        }

        private void panel1_Paint(object sender, PaintEventArgs e)
        {

        }

        private void button7_Click(object sender, EventArgs e)
        {
            string imgFile = "";
            string labelFile = "";

            openFileDialog1.Filter = "Test data (Image)|*.*";
            openFileDialog1.Title = "Open Test images file";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                imgFile = openFileDialog1.FileName;
            }
            else
            {
                return;
            }

            openFileDialog1.Filter = "Test data (Label)|*.*";
            openFileDialog1.Title = "Open Test labels file";
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                labelFile = openFileDialog1.FileName;
            }
            else
            {
                return;
            }

            List<TrainingSuite.TrainingData> trainingData = new List<TrainingSuite.TrainingData>();

            LoadTestDataFromFiles(trainingData, labelFile, imgFile);

            int success = 0;
            for (int i = 0; i < trainingData.Count; i++)
            {
                var output = network.Compute(mathLib, trainingData[i].input);

                int resultIdx = ClassifyOutput(output);
                int expectedIdx = ClassifyOutput(trainingData[i].desiredOutput);
                if (resultIdx == expectedIdx)
                    ++success;

            }

            float perc = ((float)success / (float)trainingData.Count) * 100.0f;
            MessageBox.Show("Test completed with " + trainingData.Count + " examples. Successful were: " + success + " (" + perc + "%)", "Test complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
