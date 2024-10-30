**ClippyAI**
================

**Overview**

ClippyAI is an innovative productivity tool that revolutionizes the way you interact with your computer. This
application takes notes from your clipboard and sends them along with a task to Ollama, a popular local AI service.
After being processed, the response will be available in the clipboard and can be pasted into any application the user is using.

**Features**

* **Highest Level of Data Privacy**: Your data stays your data and nothing leaves your PC. After installation, you can
even use this tool completely offline.
* **Clipboard Integration**: ClippyAI seamlessly integrates with your system's clipboard, capturing text instantly
after it is copied or cut. After activation, te clipboard's content will then be sent along with a task to the Ollama API. After execution, the output will be available in the clipboard, too.
* **Desktop Notifications**: System notifications inform the user about the task status.
* **Cross-Platform Support**: Developed with .NET and Avalonia, ClippyAI runs on Windows and Linux (X11), ensuring that users can enjoy its benefits regardless of their platform.

**Example Use Cases**

* Copy an email and let ClippyAI write the response
* Copy some code from an IDE to find an error
* Explain a compilcated text in more easy words
* Translate a text to another language
* Summarize a long text in a few sentences

![Clippy Example](./Images/clippy.png)

**Getting Started**

1. Install Ollama from https://ollama.com.
2. Run `ollama pull gemma2` on the command line to install the Gemma2 AI model or any other model on your PC.
3. Download and extract the ZIP file from the latest release.
4. Run ''setup.exe'' on Windows or the .deb/.rpm packages on Linux to install the application.
5. Run ''ClippyAI'' from the start menu of your OS.

**Using ClippyAI**

1. Choose the task from the dropdown list.
2. Copy or cut some text from an application (e.g., email, chat, or document) via [Ctrl]+[C].
3. Click ''Send'' or use the keyboard shortcut [Ctrl]+[Alt]+[C] to send the clipboard contents with the task to the local LLM.
4. Review or paste ([Ctrl]+[V]) your generated task in the application where you need it.

**Disclaimer**

This tool is in an early development phase. Use it at your own risk. We take no responsibility, if it accidently deletes or overwrites your currently opened documents. (Take special care with the experimental keyboard output.

If you encounter any issues or having questions or new ideas about using ClippyAI, please open an issue here on GitHub.

**Future Plans**

Go to the issues page on this Github repo to see things I've planned for the future.


**Developers Wanted!**

ClippyAI is an open-source project that relies on contributions from passionate developers like you. If you're interested in joining the ClippyAI community and contributing to its development, here are some ways you can get
involved:

* **Bug Fixing**: Help me squash those pesky bugs that prevent users from enjoying the full potential of ClippyAI.
* **Feature Development**: There are several things in my mind on how to improve ClippyAI. Take a look at the issues page for open enhancements. You may also suggest new features or enhancements.
* **Testing**: Put ClippyAI through its paces and help me to identify areas where it can be improved.

To get started with contributing to ClippyAI, please submit your pull requests.

**Licensing & Terms**

ClippyAI is distributed under the terms of the [MIT License](/LICENSE.md). By using ClippyAI, you agree to abide by
the terms and conditions outlined in the license.

