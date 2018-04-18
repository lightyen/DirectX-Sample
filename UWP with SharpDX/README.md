# DirectX-Sample
自我學習DirectX征途之中所產生出的範例程式

------



## 建置專案

Visual Studio 2017. Windows 10 SDK.

首次建置前要先**還原Nuget**為了下載必要的相依套件

<img src="https://i.imgur.com/WEcAJNP.png"/>

記得注意平台，如果目標平台是**x64**就選**x64**

<img src="https://i.imgur.com/JV6pr68.png" />

假如有遇到編譯Shader時發生找不到**fxc.exe**的問題如下圖：

<img src="https://i.imgur.com/c4qXWkU.png" />

錯誤提示說明找不到**fxc.exe**,解決方法是：

從**C:\Program Files (x86)\Windows Kits\10\Bin\10.0.xxx\x86**把**fxc.exe**複製一份到**C:\Program Files (x86)\Windows Kits\10\bin\x86**即可。

據說是HLSL編譯工具把其所參考的路徑給寫死了，所以只會參考那個**x86**資料夾。

