EpgDataCap_Bon
==============
**BonDriver based multifunctional EPG software**

Documents are stored in the 'Document' directory.  
Configuration files are stored in the 'ini' directory.

**このフォークについて**

このフォークは、xtne6fさんのフォーク([xtne6f@work-plus-s](https://github.com/xtne6f/EDCB/tree/work-plus-s))にちょびっとだけパッチを追加するブランチ(フォーク)です。機能に関する説明やビルド方法などはフォーク元を参照してください。フォーク元との関係は[xtne6f@NetworkGraph](https://github.com/xtne6f/EDCB/network)で確認することが出来ます。

**主な変更点について**

このフォークでは主にEpgTimerへの変更を行っています。  
参考【[各画面キャプチャ](https://tkntrec.github.io/EDCB_PrtSc)】

* 自動予約登録に合わせて予約を変更するオプションを追加した。
* 右クリックメニューに項目を追加し、表示/非表示を選択出来るようにした。【[設定画面](https://tkntrec.github.io/EDCB_PrtSc/#i44)】  
ショートカットキーを変更したい場合は、設定ファイル(XML)で直接指定してください。
* 予約などの情報を簡易検索できるダイアログを追加した。【[画面イメージ](https://tkntrec.github.io/EDCB_PrtSc/#i161)】
* その他各画面で細かい変更を行っています。
* 設定画面がなく設定ファイル(XML)で直接指定するオプションなどの説明を含むコミット
  * [ff60480](https://github.com/tkntrec/EDCB/commit/ff6048074a4a609fb22c78361682a3cb4cf4a593) 予約情報等の強制更新(F5)を追加(仮)
  * その他UIのないXMLオプションはフォーク元のドキュメントを参照。  
(ExecBat → [xtne6f@Readme_EpgTimer.txt](https://github.com/xtne6f/EDCB/blob/work-plus-s/Document/Readme_EpgTimer.txt)+[Readme_Mod.txt](https://github.com/xtne6f/EDCB/blob/work-plus-s/Document/Readme_Mod.txt)、NoSendClose → [xtne6f@fedc409](https://github.com/xtne6f/EDCB/commit/fedc409ecc5d1393b9df892a273541cbe4c7b149))

**ブランチついて**

このフォークのブランチは再作成(リベース)などで構成が変わることがあります。  
[branch:my-build](https://github.com/tkntrec/EDCB/tree/my-build)以外、特にビルドする意味はありません。  
[branch:my-ui](https://github.com/tkntrec/EDCB/tree/my-ui)はフォーク元[xtne6f@work-plus-s](https://github.com/xtne6f/EDCB/tree/work-plus-s)とのEpgTimer側の差分、[branch:my-work](https://github.com/tkntrec/EDCB/tree/my-work)はその他部分(EpgTimerSrv側)の差分です。

**注意点**

2016/9/13までのビルドからそれより新しいビルドへ変更すると、キーワード予約で「番組長で絞り込み」及び「同一番組無効予約で同一サービス確認を省略するオプション」を使用している場合、それらの設定が一度クリアされます。  
(オプションを使用していなければ影響ありません)。  

* データ移行方法(非推奨) → [キーワード予約移行方法.txt](https://github.com/tkntrec/EDCB/files/491007/default.txt)

**xtne6f版EpgTimerSrvでの使用について**

このフォークのEpgTimerは、フォーク元([xtne6f@work-plus-s](https://github.com/xtne6f/EDCB/tree/work-plus-s))のEpgTimerSrvとの組み合わせでも使用することができます。互換動作をサポートしていただいたxtne6f氏には感謝。

* xtne6f版EpgTimerSrvの設定画面→[その他]の「EpgTimerSrvの応答をtkntrec版互換にする」をチェックし、EpgTimerSrvを再起動してください。
* デフォルトで互換モードになっていない以外は、基本的にこのフォークのものと同じです。  
差分の確認→https://github.com/xtne6f/EDCB/compare/work-plus-s...tkntrec:my-work