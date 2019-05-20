﻿using Macademy;
using Macademy.OpenCL;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModuleTests
{
    public static class Utils
    {
        public static void TestTraining(float[] referenceOutput, IErrorFunction errorFunc, TrainingSuite.TrainingConfig.Regularization regularization, float regularizationLambda, float learningRate)
        {
            List<int> layerConfig = new List<int>();
            layerConfig.Add(5);
            layerConfig.Add(33);
            layerConfig.Add(12);
            layerConfig.Add(51);
            layerConfig.Add(5);

            Network network = Network.CreateNetworkFromJSON(Testing.Properties.Resources.ReferenceNetwork1JSON);

            #region Training
            List<TrainingSuite.TrainingData> trainingData = new List<TrainingSuite.TrainingData>();
            for (int i = 0; i < 1000; i++)
            {
                float[] input = new float[layerConfig[0]];
                float[] desiredOutput = new float[layerConfig[layerConfig.Count - 1]];

                input[(i * 13426) % 5] = 1.0f;
                desiredOutput[(i * 13426) % 5] = 1.0f;

                trainingData.Add(new TrainingSuite.TrainingData(input, desiredOutput));
            }

            TrainingSuite suite = new TrainingSuite(trainingData);
            suite.config.epochs = 2;
            suite.config.shuffleTrainingData = false;
            suite.config.miniBatchSize = 13;

            suite.config.costFunction = errorFunc;
            suite.config.regularization = regularization;
            suite.config.regularizationLambda = regularizationLambda;
            suite.config.learningRate = learningRate;

            var promise = network.Train(suite, new Calculator());

            promise.Await();
            #endregion

            float[] testInput = new float[] { 0.3f, 0.4f, 0.6f, 0.1f, 0.5f };
            var result = network.Compute(testInput, new Calculator());
            
            Utils.CheckNetworkError(result, referenceOutput);
        }

        public static void TestOpenCLTrainingWithConfig(IErrorFunction errorFunc, TrainingSuite.TrainingConfig.Regularization regularization, float regularizationLambda, float learningRate)
        {
            List<int> layerConfig = new List<int>();
            layerConfig.Add(10);
            layerConfig.Add(512);
            layerConfig.Add(12);
            layerConfig.Add(3);
            layerConfig.Add(51);
            layerConfig.Add(30);

            Network networkReference = Network.CreateNetworkInitRandom(layerConfig.ToArray(), new SigmoidActivation());
            var jsonData = networkReference.ExportToJSON();
            Network networkCpuTrained = Network.CreateNetworkFromJSON(jsonData);
            Network networkOpenCLTrained = Network.CreateNetworkFromJSON(jsonData);

            Calculator cpuCalculator = new Calculator();
            Calculator openCLCalculator = new Calculator(ComputeDevice.GetDevices()[0]);

            var rnd = new Random();
            List<TrainingSuite.TrainingData> trainingData = new List<TrainingSuite.TrainingData>();
            for (int i = 0; i < 1000; i++)
            {
                float[] input = new float[layerConfig[0]];
                float[] output = new float[layerConfig[layerConfig.Count - 1]];

                var idx = rnd.Next(0, input.Length);
                input[rnd.Next(0, input.Length)] = 1.0f;

                for (int j = 0; j < 10; j++)
                {
                    output[j * 3 + 0] = idx * 0.1f;
                    output[j * 3 + 1] = 1.0f - (idx * 0.1f);
                    output[j * 3 + 2] = idx * 0.05f;
                }

                trainingData.Add(new TrainingSuite.TrainingData(input, output));
            }

            TrainingSuite suite = new TrainingSuite(trainingData);
            suite.config.epochs = 1;
            suite.config.shuffleTrainingData = false;
            suite.config.miniBatchSize = 13;

            suite.config.costFunction = errorFunc;
            suite.config.regularization = regularization;
            suite.config.regularizationLambda = regularizationLambda;
            suite.config.learningRate = learningRate;

            var promise1 = networkCpuTrained.Train(suite, cpuCalculator);
            var promise2 = networkOpenCLTrained.Train(suite, openCLCalculator);

            promise1.Await();
            promise2.Await();

            Assert.IsTrue(promise1.IsReady() && promise2.IsReady());

            float[] testInput = new float[layerConfig[0]];

            var cpuTrainedOutput = networkCpuTrained.Compute(testInput, cpuCalculator);
            var openCLTrainedOutput = networkOpenCLTrained.Compute(testInput, cpuCalculator);

            CheckNetworkError(cpuTrainedOutput, openCLTrainedOutput);
        }

        public static void CheckNetworkError(float[] a, float[] b, double errorThreshold = 0.00001)
        {
            double error = 0;

            if (a.Length != b.Length)
                Assert.Fail("Network output sizes do not match!");

            for (int i = 0; i < a.Length; i++)
            {
                error += Math.Abs((double)a[i] - (double)b[i]);
            }

            var meanError = (error / a.Length);
            if (meanError > errorThreshold)
                Assert.Fail("Networks do not match. Error was: " + meanError);
        }
    }
}
