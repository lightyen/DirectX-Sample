# DirectX-Sample
自我學習DirectX征途之中所產生出的範例程式

------

敝人對於DirectX的認知僅僅是過去玩PC GAME時經常會安裝的一個程式庫，對於內部開發或什麼阿什麼等等等，可說是完全沒有概念。(~~再說大學讀的計算機圖學全部丟還給教授了~~)

最近有點心血來潮想要研究看看，而且做遊戲開發可說是碼農的夢想(?，但是又苦於DirectX發展歷史悠久，不知道從何處著手，許多眉眉角角可是困擾了好一陣子。

光是要在螢幕上顯示個三角形就弄到快吐血，初始化DirectX環境就不知道寫了幾百行。但也沒有想過要買書，畢竟只是一時興趣。

這個呢主要想用的版本是DirectX 11，通常windows 8.1以上應該會自帶DirectX 11(吧?，windows 7則需要SP1。

當然UWP是不能跑在Windows 7上的，想用的話就更新吧更新吧更新吧更新吧。

## 程式碼目錄

分為兩個版本：

- C++

- C#

C++的用的是傳統WinAPI視窗，執行速度較快，但開發環境複雜，且網路上範例多到有點泡沫化，還得注意他們的執行環境以及DirectX版本。

C#用的是UWP搭配SharpDX來實作的，執行速度不差，且有managed code可以幫助記憶體回收；缺點是SharpDX學習不易，官方文件缺乏，需要熟悉過C++的開發才比較容易上手。



## 延伸閱讀

[Where is the DirectX SDK?](https://msdn.microsoft.com/en-us/library/windows/desktop/ee663275(v=vs.85).aspx)

[Windows10 SDK](https://developer.microsoft.com/zh-tw/windows/downloads/windows-10-sdk)