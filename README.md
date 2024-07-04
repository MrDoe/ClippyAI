**ClippyAI**
================

**Overview**

ClippyAI is an innovative productivity tool that revolutionizes the way you interact with your computer. This
application takes notes from your clipboard and sends them along with a task to Ollama, a popular local AI service.
After processed, the response will be either available in the clipboard, or will automatically be typed in 
the application the user is running.

**Features**

* **Highest Level of Data Privacy**: Your data stays your data and nothing leaves your PC. After installation, you can
even use this tool completely offline.
* **Clipboard Integration**: ClippyAI seamlessly integrates with your system's clipboard, capturing text instantly
after it is copied or cut. Users can send the clipboard contents along with one of the predefined or a custom task
to the Ollama API.
* **Keyboard Simulation**: The response is either sent to an integrated output field or can automatically be typed
into the application window where the user is working. Alternatively, you can just send the output back to the clipboard.
* **Cross-Platform Support**: Developed with .NET and Avalonia, ClippyAI runs on Windows and Linux (X11)
ensuring that users can enjoy its benefits regardless of their platform.

**Example Use Cases**

* Copy an email and let ClippyAI write the response
* Copy some code from an IDE to find an error
* Explain a compilcated text in more easy words
* Translate text to another language

**Getting Started**

1. Install Ollama from https://ollama.com.
2. Run `ollama pull llama3` on the command line to install the llama3 AI model on your PC.
3. Clone this repository and build and run this tool.
4. Check the configuration in App.config. The standard values should work fine for most installations.
5. Run the application.

**Using ClippyAI**

1. To create a task, simply copy or cut some text from another application (e.g., email, chat, or document).
2. Choose the desired task type.
3. Click ''Send'' to send the task to the local Ollama API.
4. Review and edit your generated task as needed.

**Disclaimer**

This tool is in an early development phase. Use it at your own risk. We take no responsibility, if it accidently
deletes or overwrites your currently opened documents.
If you encounter any issues or have questions or new ideas about using ClippyAI, please open an issue here on GitHub.

**Future Plans**

There are several things in my mind on how to improve ClippyAI:
* Publish executables/installer packages for Windows and Linux.
* Edit ClippyAI's configuration from the UI.
* Bind ClippyAI to a global hotkey like hitting [Ctrl]+[C] twice in a short period of time.
* Don't start typing after another global hotkey has been pressed.
* Advanced task configuration like changing the system prompt, temperature and model for every task.
* Create jobs, i. e. a main task, which is divided into several sub-tasks using different AI agents and settings. 
* Show a notification and/or play a sound when an response of ClippyAI is ready.

**Developers Wanted!**

ClippyAI is an open-source project that relies on contributions from passionate developers like you. If you're
interested in joining the ClippyAI community and contributing to its development, here are some ways you can get
involved:

* **Bug Fixing**: Help me squash those pesky bugs that prevent users from enjoying the full potential of ClippyAI.
* **Feature Development**: Suggest new features or enhancements that could take ClippyAI to the next level. If
you're skilled in a particular area, I might need your expertise!
* **Testing**: Put ClippyAI through its paces and help me to identify areas where it can be improved.

To get started with contributing to ClippyAI, please submit your pull requests.

**Licensing & Terms**

ClippyAI is distributed under the terms of the [MIT License](/LICENSE.md). By using ClippyAI, you agree to abide by
the terms and conditions outlined in the license.

