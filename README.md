# MsTestTrxLogger
Logger implementation for vstest.console.exe that replicates the TRX format of mstest.exe.

## Background

The tool for running MsTest unit tests from the command line used to be `mstest.exe`.  
Later that was replaced by `vstest.console.exe`, and `mstest.exe` is now considered deprecated.

The output format of mstest.exe was an XML format called TRX. On the other hand, vstest.console.exe supports multiple *loggers*, of which one is called "trx", which is supposed to produce the same format as mstest.exe did.  
However, there are a couple of differences between the old mstest.exe output, and the vstest.console.exe trx logger output.  
These differences can cause some problems, one that I encountered is that SpecFlow cannot generate its HTML report properly any more (details [here](https://github.com/techtalk/SpecFlow/issues/278)).

The logger implemented in this repository replicates the format of the old TRX produced by `mstest.exe`.  
(Some more information on the background can be found in [this blog post](http://blog.markvincze.com/how-to-fix-the-empty-specflow-html-report-problem-with-vstest-console-exe/).)

## Usage

In order to use the logger, you need to copy its binaries to the folder `C:\Program Files (x86)\Microsoft Visual Studio 12.0\Common7\IDE\CommonExtensions\Microsoft\TestWindow\Extensions` (where you have to modify the 12.0 version number to the version of the `vstest.console.exe` you're using (12.0 is for Visual Studio 2013).

Then you have to specify the logger to use when executing the tool with the `Logger` parameter.

    vstest.console.exe ... /Logger:MsTestTrxLogger

### Getting the binaries

You can get the binaries either by cloning this repo and building the project, or by downloading them from the release directory ([link](https://github.com/markvincze/MsTestTrxLogger/raw/master/release/MsTestTrxLogger%201.0%20binaries.zip)).
