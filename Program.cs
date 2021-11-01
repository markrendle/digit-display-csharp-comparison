﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace digits
{
    class Program
    {
        static async Task Main(string[] args)
        {
            int offset = 1000;
            int count = 300;
            int threads = 15;

            var start = DateTime.Now;
            List<Prediction> errors = new();

            var (training, validation) = FileLoader.GetData("train.csv", offset, count);
            Console.Clear();
            Console.WriteLine("Data Load Complete...");

            var classifier = new EuclideanClassifier(training);

            var chunks = FileLoader.ChunkData(validation, threads);

            var channel = Channel.CreateUnbounded<Prediction>();
            var listener = Listen(channel.Reader, errors);

            List<Task> producers = new();
            for (int i = 0; i < chunks.Count; i++)
            {
                var producer = Produce(channel.Writer, classifier, chunks[i]);
                producers.Add(producer);
            }

            await Task.WhenAll(producers);
            channel.Writer.Complete();

            await listener;

            var elapsed = DateTime.Now - start;

            PrintSummary(classifier, offset, count, elapsed, errors.Count());
            Console.WriteLine("Press any key to show errors...");
            Console.ReadLine();

            foreach(var item in errors) 
            {
                DisplayImages(item, true);
            }

            PrintSummary(classifier, offset, count, elapsed, errors.Count());
        }

        static void DisplayImage(Record data, bool scroll) 
        {
            if (!scroll) {
                Console.SetCursorPosition(0, 0);
            }
            var image = Display.GetImageAsString(data.Image);
            Console.WriteLine($"{image}\n{new string('=', 56)}");
        }

        static void DisplayImages(Prediction prediction, bool scroll)
        {
            if (!scroll) {
                Console.SetCursorPosition(0, 0);
            }
            var image = Display.GetImagesAsString(prediction.Actual.Image, prediction.Predicted.Image);
            var output = $"Actual: {prediction.Actual.Value} ";
            output += new string(' ', 46);
            output += $" | Predicted: {prediction.Predicted.Value}";
            output += "\n";
            output += image;
            output += "\n";
            output += new string('=', 115);
            Console.WriteLine(output);
        }

        static void PrintSummary(Classifier classifier, int offset, int count, TimeSpan elapsed, int total_errors)
        {
            Console.WriteLine($"Using {classifier.Name} -- Offset: {offset}   Count: {count}");
            Console.WriteLine($"Total time: {elapsed}");
            Console.WriteLine($"Total errors: {total_errors}");
        }

        private static async Task Produce(ChannelWriter<Prediction> writer,
            Classifier classifier, List<Record> validation)
        {
            await Task.Run(async () =>
            {
                foreach(var item in validation)
                {
                    var result = classifier.Predict(item);
                    await writer.WriteAsync(result);
                }
            });
        }

        private static async Task Listen(ChannelReader<Prediction> reader,
            List<Prediction> log)
        {
            await foreach (Prediction prediction in reader.ReadAllAsync())
            {
                DisplayImages(prediction, false);
                if (prediction.Actual.Value != prediction.Predicted.Value)
                {
                    log.Add(prediction);
                }
            }
        }

    }
}
