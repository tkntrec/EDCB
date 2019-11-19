
// EpgDataCap_BonDlg.cpp : 実装ファイル
//

#include "stdafx.h"
#include "EpgDataCap_Bon.h"
#include "EpgDataCap_BonDlg.h"
#include "../../Common/CommonDef.h"
#include "../../Common/TimeUtil.h"
#include <shellapi.h>


// CEpgDataCap_BonDlg ダイアログ


UINT CEpgDataCap_BonDlg::taskbarCreated = 0;
BOOL CEpgDataCap_BonDlg::disableKeyboardHook = FALSE;

CEpgDataCap_BonDlg::CEpgDataCap_BonDlg()
	: m_hWnd(NULL)
	, m_hKeyboardHook(NULL)
{
	HMODULE hModule = GetModuleHandle(NULL);
	HRESULT (WINAPI* pfnLoadIconMetric)(HINSTANCE,PCWSTR,int,HICON*) =
		(HRESULT (WINAPI*)(HINSTANCE,PCWSTR,int,HICON*))GetProcAddress(GetModuleHandle(L"comctl32.dll"), "LoadIconMetric");
	if( pfnLoadIconMetric == NULL ||
	    pfnLoadIconMetric(hModule, MAKEINTRESOURCE(IDI_ICON_BLUE), LIM_SMALL, &m_hIcon) != S_OK ||
	    pfnLoadIconMetric(hModule, MAKEINTRESOURCE(IDI_ICON_BLUE), LIM_LARGE, &m_hIcon2) != S_OK ||
	    pfnLoadIconMetric(hModule, MAKEINTRESOURCE(IDI_ICON_RED), LIM_SMALL, &iconRed) != S_OK ||
	    pfnLoadIconMetric(hModule, MAKEINTRESOURCE(IDI_ICON_GREEN), LIM_SMALL, &iconGreen) != S_OK ||
	    pfnLoadIconMetric(hModule, MAKEINTRESOURCE(IDI_ICON_GRAY), LIM_SMALL, &iconGray) != S_OK ){
		m_hIcon = (HICON)LoadImage(hModule, MAKEINTRESOURCE(IDI_ICON_BLUE), IMAGE_ICON, 16, 16, LR_DEFAULTCOLOR);
		m_hIcon2 = (HICON)LoadImage(hModule, MAKEINTRESOURCE(IDI_ICON_BLUE), IMAGE_ICON, 32, 32, LR_DEFAULTCOLOR);
		iconRed = (HICON)LoadImage(hModule, MAKEINTRESOURCE(IDI_ICON_RED), IMAGE_ICON, 16, 16, LR_DEFAULTCOLOR);
		iconGreen = (HICON)LoadImage(hModule, MAKEINTRESOURCE(IDI_ICON_GREEN), IMAGE_ICON, 16, 16, LR_DEFAULTCOLOR);
		iconGray = (HICON)LoadImage(hModule, MAKEINTRESOURCE(IDI_ICON_GRAY), IMAGE_ICON, 16, 16, LR_DEFAULTCOLOR);
	}
	iconBlue = m_hIcon;

	taskbarCreated = RegisterWindowMessage(L"TaskbarCreated");

	iniView = FALSE;
	iniNetwork = TRUE;
	iniMin = FALSE;
	this->iniUDP = FALSE;
	this->iniTCP = FALSE;
}

INT_PTR CEpgDataCap_BonDlg::DoModal()
{
	return DialogBoxParam(GetModuleHandle(NULL), MAKEINTRESOURCE(IDD), NULL, DlgProc, (LPARAM)this);
}


// CEpgDataCap_BonDlg メッセージ ハンドラー
BOOL CEpgDataCap_BonDlg::OnInitDialog()
{
	// このダイアログのアイコンを設定します。アプリケーションのメイン ウィンドウがダイアログでない場合、
	//  Framework は、この設定を自動的に行います。
	SendMessage(m_hWnd, WM_SETICON, ICON_BIG, (LPARAM)m_hIcon2);	// 大きいアイコンの設定
	SendMessage(m_hWnd, WM_SETICON, ICON_SMALL, (LPARAM)m_hIcon);	// 小さいアイコンの設定

	// TODO: 初期化をここに追加します。
	this->main.ReloadSetting();

	for( int i=0; i<24; i++ ){
		WCHAR buff[32];
		swprintf_s(buff, L"%d", i);
		ComboBox_AddString(GetDlgItem(IDC_COMBO_REC_H), buff);
	}
	ComboBox_SetCurSel(GetDlgItem(IDC_COMBO_REC_H), 0);

	for( int i=0; i<60; i++ ){
		WCHAR buff[32];
		swprintf_s(buff, L"%d", i);
		ComboBox_AddString(GetDlgItem(IDC_COMBO_REC_M), buff);
	}
	ComboBox_SetCurSel(GetDlgItem(IDC_COMBO_REC_M), 0);

	fs_path appIniPath = GetModuleIniPath();

	this->minTask = GetPrivateProfileInt(L"SET", L"MinTask", 0, appIniPath.c_str());
	int initONID = -1;
	int initTSID = -1;
	int initSID = -1;
	int initOpenWait = 0;
	int initChgWait = 0;
	if( this->iniBonDriver.empty() == false &&
	    GetPrivateProfileInt(this->iniBonDriver.c_str(), L"OpenFix", 0, appIniPath.c_str()) ){
		OutputDebugString(L"強制サービス指定 設定値ロード");
		initONID = GetPrivateProfileInt(this->iniBonDriver.c_str(), L"FixONID", -1, appIniPath.c_str());
		initTSID = GetPrivateProfileInt(this->iniBonDriver.c_str(), L"FixTSID", -1, appIniPath.c_str());
		initSID = GetPrivateProfileInt(this->iniBonDriver.c_str(), L"FixSID", -1, appIniPath.c_str());
		initOpenWait = GetPrivateProfileInt(this->iniBonDriver.c_str(), L"OpenWait", 0, appIniPath.c_str());
		initChgWait = GetPrivateProfileInt(this->iniBonDriver.c_str(), L"ChgWait", 0, appIniPath.c_str());
		_OutputDebugString(L"%d,%d,%d,%d,%d", initONID, initTSID, initSID, initOpenWait, initChgWait);
	}else if( GetPrivateProfileInt(L"SET", L"OpenLast", 1, appIniPath.c_str()) ){
		initONID = GetPrivateProfileInt(L"SET", L"LastONID", -1, appIniPath.c_str());
		initTSID = GetPrivateProfileInt(L"SET", L"LastTSID", -1, appIniPath.c_str());
		initSID = GetPrivateProfileInt(L"SET", L"LastSID", -1, appIniPath.c_str());
		if( this->iniBonDriver.empty() ){
			this->iniBonDriver = GetPrivateProfileToString(L"SET", L"LastBon", L"", appIniPath.c_str());
		}
	}else if( GetPrivateProfileInt(L"SET", L"OpenFix", 0, appIniPath.c_str()) ){
		initONID = GetPrivateProfileInt(L"SET", L"FixONID", -1, appIniPath.c_str());
		initTSID = GetPrivateProfileInt(L"SET", L"FixTSID", -1, appIniPath.c_str());
		initSID = GetPrivateProfileInt(L"SET", L"FixSID", -1, appIniPath.c_str());
		if( this->iniBonDriver.empty() ){
			this->iniBonDriver = GetPrivateProfileToString(L"SET", L"FixBon", L"", appIniPath.c_str());
		}
	}

	//BonDriverの一覧取得
	int bonIndex = -1;
	wstring bon;
	EnumFindFile(GetModulePath().replace_filename(BON_DLL_FOLDER).append(L"BonDriver*.dll"), [&](UTIL_FIND_DATA& findData) -> bool {
		if( findData.isDir == false ){
			int index = ComboBox_AddString(GetDlgItem(IDC_COMBO_TUNER), findData.fileName.c_str());
			if( bonIndex < 0 || UtilComparePath(findData.fileName.c_str(), this->iniBonDriver.c_str()) == 0 ){
				bonIndex = index;
				bon = std::move(findData.fileName);
			}
		}
		return true;
	});
	if( bonIndex >= 0 ){
		ComboBox_SetCurSel(GetDlgItem(IDC_COMBO_TUNER), bonIndex);
	}

	//BonDriverのオープン
	int serviceIndex = -1;
	if( this->iniBonDriver.empty() == false ){
		//BonDriver指定時は一覧になくてもよい
		if( SelectBonDriver(this->iniBonDriver.c_str()) ){
			if( initOpenWait > 0 ){
				Sleep(initOpenWait);
			}
			serviceIndex = ReloadServiceList(initONID, initTSID, initSID);
		}
	}else{
		if( bonIndex >= 0 ){
			//一覧で選択されたものをオープン
			if( SelectBonDriver(bon.c_str()) ){
				serviceIndex = ReloadServiceList();
			}
		}else{
			SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"BonDriverが見つかりませんでした\r\n");
			BtnUpdate(GUI_OPEN_FAIL);
		}
	}

	if( serviceIndex >= 0 ){
		//チャンネル変更
		if( SelectService(this->serviceList[serviceIndex]) ){
			if( initONID >= 0 && initTSID >= 0 && initSID >= 0 && initChgWait > 0 ){
				Sleep(initChgWait);
			}
		}
	}

	//ウインドウの復元
	WINDOWPLACEMENT Pos;
	Pos.length = sizeof(WINDOWPLACEMENT);
	int left = GetPrivateProfileInt(L"SET_WINDOW", L"left", INT_MAX, appIniPath.c_str());
	int top = GetPrivateProfileInt(L"SET_WINDOW", L"top", INT_MAX, appIniPath.c_str());
	if( left != INT_MAX && top != INT_MAX && GetWindowPlacement(m_hWnd, &Pos) ){
		Pos.flags = 0;
		Pos.showCmd = this->iniMin ? SW_SHOWMINNOACTIVE : SW_SHOW;
		int width = GetPrivateProfileInt(L"SET_WINDOW", L"width", 0, appIniPath.c_str());
		int height = GetPrivateProfileInt(L"SET_WINDOW", L"height", 0, appIniPath.c_str());
		if( width > 0 && height > 0 ){
			Pos.rcNormalPosition.right = left + width;
			Pos.rcNormalPosition.bottom = top + height;
		}else{
			Pos.rcNormalPosition.right += left - Pos.rcNormalPosition.left;
			Pos.rcNormalPosition.bottom += top - Pos.rcNormalPosition.top;
		}
		Pos.rcNormalPosition.left = left;
		Pos.rcNormalPosition.top = top;
		SetWindowPlacement(m_hWnd, &Pos);
	}
	SetTimer(TIMER_STATUS_UPDATE, 1000, NULL);
	SetTimer(TIMER_INIT_DLG, 1, NULL);
	this->main.SetHwnd(GetSafeHwnd());

	if( this->iniNetwork == TRUE ){
		if( this->iniUDP == TRUE || this->iniTCP == TRUE ){
			if( this->iniUDP == TRUE ){
				Button_SetCheck(GetDlgItem(IDC_CHECK_UDP), BST_CHECKED);
			}
			if( this->iniTCP == TRUE ){
				Button_SetCheck(GetDlgItem(IDC_CHECK_TCP), BST_CHECKED);
			}
		}else{
			Button_SetCheck(GetDlgItem(IDC_CHECK_UDP), GetPrivateProfileInt(L"SET", L"ChkUDP", 0, appIniPath.c_str()));
			Button_SetCheck(GetDlgItem(IDC_CHECK_TCP), GetPrivateProfileInt(L"SET", L"ChkTCP", 0, appIniPath.c_str()));
		}
	}

	ReloadNWSet();

	this->main.StartServer();

	return TRUE;  // フォーカスをコントロールに設定した場合を除き、TRUE を返します。
}


void CEpgDataCap_BonDlg::OnSysCommand(UINT nID, LPARAM lParam, BOOL* pbProcessed)
{
	// TODO: ここにメッセージ ハンドラー コードを追加するか、既定の処理を呼び出します。
	if( nID == SC_CLOSE ){
		if( this->main.IsRec() == TRUE ){
			WCHAR caption[128] = L"";
			GetWindowText(m_hWnd, caption, 128);
			disableKeyboardHook = TRUE;
			int result = MessageBox( m_hWnd, L"録画中ですが終了しますか？", caption, MB_YESNO | MB_ICONQUESTION );
			disableKeyboardHook = FALSE;
			if( result == IDNO ){
				*pbProcessed = TRUE;
				return ;
			}
			this->main.StopReserveRec();
			this->main.StopRec();
		}
	}
}


void CEpgDataCap_BonDlg::OnDestroy()
{
	this->main.StopServer();
	this->main.CloseBonDriver();
	KillTimer(TIMER_STATUS_UPDATE);

	KillTimer(RETRY_ADD_TRAY);
	DeleteTaskBar(GetSafeHwnd(), TRAYICON_ID);

	WINDOWPLACEMENT Pos;
	Pos.length = sizeof(WINDOWPLACEMENT);
	GetWindowPlacement(m_hWnd, &Pos);

	fs_path appIniPath = GetModuleIniPath();

	WritePrivateProfileInt(L"SET_WINDOW", L"top", Pos.rcNormalPosition.top, appIniPath.c_str());
	WritePrivateProfileInt(L"SET_WINDOW", L"left", Pos.rcNormalPosition.left, appIniPath.c_str());
	WritePrivateProfileInt(L"SET_WINDOW", L"bottom", Pos.rcNormalPosition.bottom, appIniPath.c_str());
	WritePrivateProfileInt(L"SET_WINDOW", L"right", Pos.rcNormalPosition.right, appIniPath.c_str());

	int selONID = -1;
	int selTSID = -1;
	int selSID = -1;
	WCHAR bon[512] = L"";

	GetWindowText(GetDlgItem(IDC_COMBO_TUNER), bon, 512);
	int sel = ComboBox_GetCurSel(GetDlgItem(IDC_COMBO_SERVICE));
	if( sel != CB_ERR ){
		DWORD index = (DWORD)ComboBox_GetItemData(GetDlgItem(IDC_COMBO_SERVICE), sel);
		selONID = this->serviceList[index].originalNetworkID;
		selTSID = this->serviceList[index].transportStreamID;
		selSID = this->serviceList[index].serviceID;
	}

	WritePrivateProfileInt(L"SET", L"LastONID", selONID, appIniPath.c_str());
	WritePrivateProfileInt(L"SET", L"LastTSID", selTSID, appIniPath.c_str());
	WritePrivateProfileInt(L"SET", L"LastSID", selSID, appIniPath.c_str());
	WritePrivateProfileString(L"SET", L"LastBon", bon, appIniPath.c_str());
	WritePrivateProfileInt(L"SET", L"ChkUDP", Button_GetCheck(GetDlgItem(IDC_CHECK_UDP)), appIniPath.c_str());
	WritePrivateProfileInt(L"SET", L"ChkTCP", Button_GetCheck(GetDlgItem(IDC_CHECK_TCP)), appIniPath.c_str());

	// TODO: ここにメッセージ ハンドラー コードを追加します。
}


void CEpgDataCap_BonDlg::OnTimer(UINT_PTR nIDEvent)
{
	// TODO: ここにメッセージ ハンドラー コードを追加するか、既定の処理を呼び出します。
	switch(nIDEvent){
		case TIMER_INIT_DLG:
			{
				KillTimer( TIMER_INIT_DLG );
				if( this->iniMin == TRUE && this->minTask == TRUE){
				    ShowWindow(m_hWnd, SW_HIDE);
				}
			}
			break;
		case TIMER_STATUS_UPDATE:
			{
				SetThreadExecutionState(ES_SYSTEM_REQUIRED);

				int iLine = Edit_GetFirstVisibleLine(GetDlgItem(IDC_EDIT_STATUS));
				float signal;
				int space;
				int ch;
				ULONGLONG drop = 0;
				ULONGLONG scramble = 0;
				vector<NW_SEND_INFO> udpSendList = this->main.GetSendUDPList();
				vector<NW_SEND_INFO> tcpSendList = this->main.GetSendTCPList();
				this->main.GetViewStatusInfo(&signal, &space, &ch, &drop, &scramble);

				wstring statusLog = L"";
				if( space >= 0 && ch >= 0 ){
					Format(statusLog, L"Signal: %.02f Drop: %lld Scramble: %lld  space: %d ch: %d", signal, drop, scramble, space, ch);
				}else{
					Format(statusLog, L"Signal: %.02f Drop: %lld Scramble: %lld", signal, drop, scramble);
				}
				statusLog += L"\r\n";

				wstring udp = L"";
				if( udpSendList.size() > 0 ){
					udp = L"UDP送信：";
					for( size_t i=0; i<udpSendList.size(); i++ ){
						wstring buff;
						Format(buff, L":%d%ls ", udpSendList[i].port, udpSendList[i].broadcastFlag ? L"(Broadcast)" : L"");
						udp += udpSendList[i].ipString.find(L':') == wstring::npos ? udpSendList[i].ipString : L'[' + udpSendList[i].ipString + L']';
						udp += buff;
					}
					udp += L"\r\n";
				}
				statusLog += udp;

				wstring tcp = L"";
				if( tcpSendList.size() > 0 ){
					tcp = L"TCP送信：";
					for( size_t i=0; i<tcpSendList.size(); i++ ){
						wstring buff;
						Format(buff, L":%d ", tcpSendList[i].port);
						tcp += tcpSendList[i].ipString.find(L':') == wstring::npos ? tcpSendList[i].ipString : L'[' + tcpSendList[i].ipString + L']';
						tcp += buff;
					}
					tcp += L"\r\n";
				}
				statusLog += tcp;

				SetDlgItemText(m_hWnd, IDC_EDIT_STATUS, statusLog.c_str());
				Edit_Scroll(GetDlgItem(IDC_EDIT_STATUS), iLine, 0);

				wstring info = L"";
				this->main.GetEpgInfo(Button_GetCheck(GetDlgItem(IDC_CHECK_NEXTPG)), &info);
				WCHAR pgInfo[512] = L"";
				GetDlgItemText(m_hWnd, IDC_EDIT_PG_INFO, pgInfo, 512);
				if( info.substr(0, 511).compare(pgInfo) != 0 ){
					SetDlgItemText(m_hWnd, IDC_EDIT_PG_INFO, info.c_str());
				}
			}
			break;
		case TIMER_CHSCAN_STATSU:
			{
				DWORD space = 0;
				DWORD ch = 0;
				wstring chName = L"";
				DWORD chkNum = 0;
				DWORD totalNum = 0;
				CBonCtrl::JOB_STATUS status = this->main.GetChScanStatus(&space, &ch, &chName, &chkNum, &totalNum);
				if( status == CBonCtrl::ST_WORKING ){
					wstring log;
					Format(log, L"%ls (%d/%d 残り約 %d 秒)\r\n", chName.c_str(), chkNum, totalNum, (totalNum - chkNum)*10);
					SetDlgItemText(m_hWnd, IDC_EDIT_LOG, log.c_str());
				}else if( status == CBonCtrl::ST_CANCEL ){
					KillTimer(TIMER_CHSCAN_STATSU);
					SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"キャンセルされました\r\n");
				}else if( status == CBonCtrl::ST_COMPLETE ){
					KillTimer(TIMER_CHSCAN_STATSU);
					SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"終了しました\r\n");
					int index = ReloadServiceList();
					if( index >= 0 ){
						SelectService(this->serviceList[index]);
					}
					BtnUpdate(GUI_NORMAL);
					ChgIconStatus();

					//同じサービスが別の物理チャンネルにあるかチェック
					wstring msg = L"";
					for( size_t i=0; i<this->serviceList.size(); i++ ){
						for( size_t j=i+1; j<this->serviceList.size(); j++ ){
							if( this->serviceList[i].originalNetworkID == this->serviceList[j].originalNetworkID &&
								this->serviceList[i].transportStreamID == this->serviceList[j].transportStreamID &&
								this->serviceList[i].serviceID == this->serviceList[j].serviceID ){
									wstring log;
									Format(log, L"%ls space:%d ch:%d <=> %ls space:%d ch:%d\r\n",
										this->serviceList[i].serviceName.c_str(),
										this->serviceList[i].space,
										this->serviceList[i].ch,
										this->serviceList[j].serviceName.c_str(),
										this->serviceList[j].space,
										this->serviceList[j].ch);
									msg += log;
									break;
							}
						}
					}
					if( msg.size() > 0){
						wstring log = L"同一サービスが複数の物理チャンネルで検出されました。\r\n受信環境のよい物理チャンネルのサービスのみ残すように設定を行ってください。\r\n正常に録画できない可能性が出てきます。\r\n\r\n";
						log += msg;
						MessageBox(m_hWnd, log.c_str(), NULL, MB_OK);
					}
				}else{
					KillTimer(TIMER_CHSCAN_STATSU);
				}
			}
			break;
		case TIMER_EPGCAP_STATSU:
			{
				SET_CH_INFO info;
				CBonCtrl::JOB_STATUS status = this->main.GetEpgCapStatus(&info);
				if( status == CBonCtrl::ST_WORKING ){
					ReloadServiceList(info.ONID, info.TSID, info.SID);
					this->main.SetSID(info.SID);
					SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"EPG取得中\r\n");
				}else if( status == CBonCtrl::ST_CANCEL ){
					KillTimer(TIMER_EPGCAP_STATSU);
					SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"キャンセルされました\r\n");
				}else if( status == CBonCtrl::ST_COMPLETE ){
					KillTimer(TIMER_EPGCAP_STATSU);
					SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"終了しました\r\n");
					BtnUpdate(GUI_NORMAL);
					ChgIconStatus();
				}else{
					KillTimer(TIMER_EPGCAP_STATSU);
				}
			}
			break;
		case TIMER_REC_END:
			{
				this->main.StopRec();
				KillTimer(TIMER_REC_END);
				SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"録画停止しました\r\n");
				BtnUpdate(GUI_NORMAL);
				Button_SetCheck(GetDlgItem(IDC_CHECK_REC_SET), BST_UNCHECKED);
				ChgIconStatus();
			}
			break;
		case RETRY_ADD_TRAY:
			{
				KillTimer(RETRY_ADD_TRAY);
				wstring buff=L"";
				wstring bonFile = L"";
				this->main.GetOpenBonDriver(&bonFile);
				WCHAR szBuff2[256]=L"";
				GetWindowText(GetDlgItem(IDC_COMBO_SERVICE), szBuff2, 256);
				Format(buff, L"%ls ： %ls", bonFile.c_str(), szBuff2);

				HICON setIcon = this->iconBlue;
				if( this->main.IsRec() == TRUE ){
					setIcon = this->iconRed;
				}else if( this->main.GetEpgCapStatus(NULL) == CBonCtrl::ST_WORKING ){
					setIcon = this->iconGreen;
				}else if( this->main.GetOpenBonDriver(NULL) == FALSE ){
					setIcon = this->iconGray;
				}
		
				if( AddTaskBar( GetSafeHwnd(),
						WM_TRAY_PUSHICON,
						TRAYICON_ID,
						setIcon,
						buff ) == FALSE ){
							SetTimer(RETRY_ADD_TRAY, 5000, NULL);
				}
			}
			break;
		case TIMER_TRY_STOP_SERVER:
			if( this->main.StopServer(true) ){
				KillTimer(TIMER_TRY_STOP_SERVER);
				OutputDebugString(L"CmdServer stopped\r\n");
				EndDialog(m_hWnd, IDCANCEL);
			}
			break;
		default:
			break;
	}
}


void CEpgDataCap_BonDlg::OnSize(UINT nType, int cx, int cy)
{
	// TODO: ここにメッセージ ハンドラー コードを追加します。
	if( nType == SIZE_MINIMIZED && this->minTask == TRUE){
		SetTimer(RETRY_ADD_TRAY, 0, NULL);
		if(!this->iniMin) ShowWindow(m_hWnd, SW_HIDE);
	}
}


LRESULT CEpgDataCap_BonDlg::WindowProc(UINT message, WPARAM wParam, LPARAM lParam)
{
	// TODO: ここに特定なコードを追加するか、もしくは基本クラスを呼び出してください。
	switch(message){
	case WM_RESERVE_REC_START:
		{
			BtnUpdate(GUI_OTHER_CTRL);
			WCHAR log[512 + 64] = L"";
			GetDlgItemText(m_hWnd, IDC_EDIT_LOG, log, 512);
			if( wstring(log).find(L"予約録画中\r\n") == wstring::npos ){
				wcscat_s(log, L"予約録画中\r\n");
				SetDlgItemText(m_hWnd, IDC_EDIT_LOG, log);
			}
			ChgIconStatus();
		}
		break;
	case WM_RESERVE_REC_STOP:
		{
			BtnUpdate(GUI_NORMAL);
			SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"予約録画終了しました\r\n");
			ChgIconStatus();
		}
		break;
	case WM_RESERVE_EPGCAP_START:
		{
			SetTimer(TIMER_EPGCAP_STATSU, 1000, NULL);
			BtnUpdate(GUI_CANCEL_ONLY);
			ChgIconStatus();
		}
		break;
	case WM_RESERVE_EPGCAP_STOP:
		{
			ChgIconStatus();
		}
		break;
	case WM_CHG_TUNER:
		{
			wstring bonDriver = L"";
			this->main.GetOpenBonDriver(&bonDriver);
			for( int i = 0; i < ComboBox_GetCount(GetDlgItem(IDC_COMBO_TUNER)); i++ ){
				WCHAR buff[512];
				if( ComboBox_GetLBTextLen(GetDlgItem(IDC_COMBO_TUNER), i) < 512 &&
				    ComboBox_GetLBText(GetDlgItem(IDC_COMBO_TUNER), i, buff) > 0 &&
				    UtilComparePath(buff, bonDriver.c_str()) == 0 ){
					ComboBox_SetCurSel(GetDlgItem(IDC_COMBO_TUNER), i);
					break;
				}
			}
			ChgIconStatus();
		}
		break;
	case WM_CHG_CH:
		{
			WORD ONID;
			WORD TSID;
			WORD SID;
			this->main.GetCh(&ONID, &TSID, &SID);
			ReloadServiceList(ONID, TSID, SID);
			ChgIconStatus();
		}
		break;
	case WM_RESERVE_REC_STANDBY:
		{
			if( wParam == 1 ){
				BtnUpdate(GUI_REC_STANDBY);
				SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"予約録画待機中\r\n");
			}else if( wParam == 2 ){
				BtnUpdate(GUI_NORMAL);
				SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"視聴モード\r\n");
			}else{
				BtnUpdate(GUI_NORMAL);
				SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"");
			}
		}
		break;
	case WM_INVOKE_CTRL_CMD:
		this->main.CtrlCmdCallbackInvoked();
		break;
	case WM_VIEW_APP_OPEN:
		this->main.ViewAppOpen();
		break;
	case WM_TRAY_PUSHICON:
		{
			//タスクトレイ関係
			switch(LOWORD(lParam)){
				case WM_LBUTTONDOWN:
					{
						this->iniMin = FALSE;
						ShowWindow(m_hWnd, SW_RESTORE);
						SetForegroundWindow(m_hWnd);
						KillTimer(RETRY_ADD_TRAY);
						DeleteTaskBar(GetSafeHwnd(), TRAYICON_ID);
					}
					break ;
				default :
					break ;
				}
		}
		break;
	default:
		break;
	}

	return 0;
}


BOOL CEpgDataCap_BonDlg::AddTaskBar(HWND wnd, UINT msg, UINT id, HICON icon, wstring tips)
{ 
	BOOL ret=TRUE;
	NOTIFYICONDATA data = {};

	data.cbSize = sizeof(NOTIFYICONDATA); 
	data.hWnd = wnd; 
	data.uID = id; 
	data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP; 
	data.uCallbackMessage = msg; 
	data.hIcon = icon; 

	wcsncpy_s(data.szTip, tips.c_str(), _TRUNCATE);
 
	ret = Shell_NotifyIcon(NIM_ADD, &data);
  
	return ret; 
}

BOOL CEpgDataCap_BonDlg::ChgTipsTaskBar(HWND wnd, UINT id, HICON icon, wstring tips)
{ 
	BOOL ret=TRUE;
	NOTIFYICONDATA data = {};

	data.cbSize = sizeof(NOTIFYICONDATA); 
	data.hWnd = wnd; 
	data.uID = id; 
	data.hIcon = icon; 
	data.uFlags = NIF_ICON | NIF_TIP; 

	wcsncpy_s(data.szTip, tips.c_str(), _TRUNCATE);
 
	ret = Shell_NotifyIcon(NIM_MODIFY, &data); 
 
	return ret; 
}

BOOL CEpgDataCap_BonDlg::DeleteTaskBar(HWND wnd, UINT id)
{ 
	BOOL ret=TRUE; 
	NOTIFYICONDATA data = {};
 
	data.cbSize = sizeof(NOTIFYICONDATA); 
	data.hWnd = wnd; 
	data.uID = id; 
         
	ret = Shell_NotifyIcon(NIM_DELETE, &data); 

	return ret; 
}

void CEpgDataCap_BonDlg::ChgIconStatus(){
	if( this->minTask == TRUE){
		wstring buff=L"";
		wstring bonFile = L"";
		this->main.GetOpenBonDriver(&bonFile);
		WCHAR szBuff2[256]=L"";
		GetWindowText(GetDlgItem(IDC_COMBO_SERVICE), szBuff2, 256);
		Format(buff, L"%ls ： %ls", bonFile.c_str(), szBuff2);

		HICON setIcon = this->iconBlue;
		if( this->main.IsRec() == TRUE ){
			setIcon = this->iconRed;
		}else if( this->main.GetEpgCapStatus(NULL) == CBonCtrl::ST_WORKING ){
			setIcon = this->iconGreen;
		}else if( this->main.GetOpenBonDriver(NULL) == FALSE ){
			setIcon = this->iconGray;
		}

		ChgTipsTaskBar( GetSafeHwnd(),
				TRAYICON_ID,
				setIcon,
				buff );
	}
}

LRESULT CEpgDataCap_BonDlg::OnTaskbarCreated(WPARAM, LPARAM)
{
	if( IsWindowVisible(m_hWnd) == FALSE && this->minTask == TRUE){
		SetTimer(RETRY_ADD_TRAY, 0, NULL);
	}

	return 0;
}

#define ENABLE_ITEM(nItem,bEnable) EnableWindow(GetDlgItem(nItem),(bEnable))

void CEpgDataCap_BonDlg::BtnUpdate(DWORD guiMode)
{
	switch(guiMode){
		case GUI_NORMAL:
			ENABLE_ITEM(IDC_COMBO_TUNER, TRUE);
			ENABLE_ITEM(IDC_COMBO_SERVICE, TRUE);
			ENABLE_ITEM(IDC_BUTTON_CHSCAN, TRUE);
			ENABLE_ITEM(IDC_BUTTON_EPG, TRUE);
			ENABLE_ITEM(IDC_BUTTON_SET, TRUE);
			ENABLE_ITEM(IDC_BUTTON_REC, TRUE);
			ENABLE_ITEM(IDC_COMBO_REC_H, FALSE);
			ENABLE_ITEM(IDC_COMBO_REC_M, FALSE);
			ENABLE_ITEM(IDC_CHECK_REC_SET, FALSE);
			Button_SetCheck(GetDlgItem(IDC_CHECK_REC_SET), BST_UNCHECKED);
			ENABLE_ITEM(IDC_BUTTON_CANCEL, FALSE);
			ENABLE_ITEM(IDC_BUTTON_VIEW, TRUE);
			break;
		case GUI_CANCEL_ONLY:
			ENABLE_ITEM(IDC_COMBO_TUNER, FALSE);
			ENABLE_ITEM(IDC_COMBO_SERVICE, FALSE);
			ENABLE_ITEM(IDC_BUTTON_CHSCAN, FALSE);
			ENABLE_ITEM(IDC_BUTTON_EPG, FALSE);
			ENABLE_ITEM(IDC_BUTTON_SET, FALSE);
			ENABLE_ITEM(IDC_BUTTON_REC, FALSE);
			ENABLE_ITEM(IDC_COMBO_REC_H, FALSE);
			ENABLE_ITEM(IDC_COMBO_REC_M, FALSE);
			ENABLE_ITEM(IDC_CHECK_REC_SET, FALSE);
			ENABLE_ITEM(IDC_BUTTON_CANCEL, TRUE);
			ENABLE_ITEM(IDC_BUTTON_VIEW, TRUE);
			break;
		case GUI_OPEN_FAIL:
			ENABLE_ITEM(IDC_COMBO_TUNER, TRUE);
			ENABLE_ITEM(IDC_COMBO_SERVICE, FALSE);
			ENABLE_ITEM(IDC_BUTTON_CHSCAN, FALSE);
			ENABLE_ITEM(IDC_BUTTON_EPG, FALSE);
			ENABLE_ITEM(IDC_BUTTON_SET, TRUE);
			ENABLE_ITEM(IDC_BUTTON_REC, FALSE);
			ENABLE_ITEM(IDC_COMBO_REC_H, FALSE);
			ENABLE_ITEM(IDC_COMBO_REC_M, FALSE);
			ENABLE_ITEM(IDC_CHECK_REC_SET, FALSE);
			ENABLE_ITEM(IDC_BUTTON_CANCEL, FALSE);
			ENABLE_ITEM(IDC_BUTTON_VIEW, TRUE);
			break;
		case GUI_REC:
			ENABLE_ITEM(IDC_COMBO_TUNER, FALSE);
			ENABLE_ITEM(IDC_COMBO_SERVICE, FALSE);
			ENABLE_ITEM(IDC_BUTTON_CHSCAN, FALSE);
			ENABLE_ITEM(IDC_BUTTON_EPG, FALSE);
			ENABLE_ITEM(IDC_BUTTON_SET, FALSE);
			ENABLE_ITEM(IDC_BUTTON_REC, FALSE);
			ENABLE_ITEM(IDC_COMBO_REC_H, TRUE);
			ENABLE_ITEM(IDC_COMBO_REC_M, TRUE);
			ENABLE_ITEM(IDC_CHECK_REC_SET, TRUE);
			Button_SetCheck(GetDlgItem(IDC_CHECK_REC_SET), BST_UNCHECKED);
			ENABLE_ITEM(IDC_BUTTON_CANCEL, TRUE);
			ENABLE_ITEM(IDC_BUTTON_VIEW, TRUE);
			break;
		case GUI_REC_SET_TIME:
			ENABLE_ITEM(IDC_COMBO_TUNER, FALSE);
			ENABLE_ITEM(IDC_COMBO_SERVICE, FALSE);
			ENABLE_ITEM(IDC_BUTTON_CHSCAN, FALSE);
			ENABLE_ITEM(IDC_BUTTON_EPG, FALSE);
			ENABLE_ITEM(IDC_BUTTON_SET, FALSE);
			ENABLE_ITEM(IDC_BUTTON_REC, FALSE);
			ENABLE_ITEM(IDC_COMBO_REC_H, FALSE);
			ENABLE_ITEM(IDC_COMBO_REC_M, FALSE);
			ENABLE_ITEM(IDC_CHECK_REC_SET, TRUE);
			ENABLE_ITEM(IDC_BUTTON_CANCEL, TRUE);
			ENABLE_ITEM(IDC_BUTTON_VIEW, TRUE);
			break;
		case GUI_OTHER_CTRL:
			ENABLE_ITEM(IDC_COMBO_TUNER, FALSE);
			ENABLE_ITEM(IDC_COMBO_SERVICE, FALSE);
			ENABLE_ITEM(IDC_BUTTON_CHSCAN, FALSE);
			ENABLE_ITEM(IDC_BUTTON_EPG, FALSE);
			ENABLE_ITEM(IDC_BUTTON_SET, FALSE);
			ENABLE_ITEM(IDC_BUTTON_REC, FALSE);
			ENABLE_ITEM(IDC_COMBO_REC_H, FALSE);
			ENABLE_ITEM(IDC_COMBO_REC_M, FALSE);
			ENABLE_ITEM(IDC_CHECK_REC_SET, FALSE);
			ENABLE_ITEM(IDC_BUTTON_CANCEL, TRUE);
			ENABLE_ITEM(IDC_BUTTON_VIEW, TRUE);
			break;
		case GUI_REC_STANDBY:
			ENABLE_ITEM(IDC_COMBO_TUNER, FALSE);
			ENABLE_ITEM(IDC_COMBO_SERVICE, FALSE);
			ENABLE_ITEM(IDC_BUTTON_CHSCAN, FALSE);
			ENABLE_ITEM(IDC_BUTTON_EPG, FALSE);
			ENABLE_ITEM(IDC_BUTTON_SET, FALSE);
			ENABLE_ITEM(IDC_BUTTON_REC, FALSE);
			ENABLE_ITEM(IDC_COMBO_REC_H, FALSE);
			ENABLE_ITEM(IDC_COMBO_REC_M, FALSE);
			ENABLE_ITEM(IDC_CHECK_REC_SET, FALSE);
			ENABLE_ITEM(IDC_BUTTON_CANCEL, FALSE);
			ENABLE_ITEM(IDC_BUTTON_VIEW, TRUE);
			break;
		default:
			break;
	}
}



void CEpgDataCap_BonDlg::OnCbnSelchangeComboTuner()
{
	// TODO: ここにコントロール通知ハンドラー コードを追加します。
	WCHAR buff[512];
	if( GetWindowText(GetDlgItem(IDC_COMBO_TUNER), buff, 512) > 0 ){
		if( SelectBonDriver(buff) ){
			int index = ReloadServiceList();
			if( index >= 0 ){
				SelectService(this->serviceList[index]);
			}
		}else{
			this->serviceList.clear();
			ComboBox_ResetContent(GetDlgItem(IDC_COMBO_SERVICE));
		}
	}
	ChgIconStatus();
}


void CEpgDataCap_BonDlg::OnCbnSelchangeComboService()
{
	// TODO: ここにコントロール通知ハンドラー コードを追加します。
	int sel = ComboBox_GetCurSel(GetDlgItem(IDC_COMBO_SERVICE));
	if( sel != CB_ERR ){
		DWORD index = (DWORD)ComboBox_GetItemData(GetDlgItem(IDC_COMBO_SERVICE), sel);
		SelectService(this->serviceList[index]);
	}
	ChgIconStatus();
}


void CEpgDataCap_BonDlg::OnBnClickedButtonSet()
{
	// TODO: ここにコントロール通知ハンドラー コードを追加します。
	CSettingDlg setDlg(m_hWnd);
	disableKeyboardHook = TRUE;
	INT_PTR result = setDlg.DoModal();
	disableKeyboardHook = FALSE;
	if( result == IDOK ){

		this->main.ReloadSetting();

		ReloadNWSet();

		WORD ONID;
		WORD TSID;
		WORD SID;
		this->main.GetCh(&ONID, &TSID, &SID);
		ReloadServiceList(ONID, TSID, SID);
		
		this->minTask = GetPrivateProfileInt(L"SET", L"MinTask", 0, GetModuleIniPath().c_str());
	}
}

void CEpgDataCap_BonDlg::ReloadNWSet()
{
	this->main.SendUDP(FALSE);
	this->main.SendTCP(FALSE);
	if( this->main.GetCountUDP() > 0 ){
		EnableWindow(GetDlgItem(IDC_CHECK_UDP), TRUE);
	}else{
		EnableWindow(GetDlgItem(IDC_CHECK_UDP), FALSE);
		Button_SetCheck(GetDlgItem(IDC_CHECK_UDP), BST_UNCHECKED);
	}
	if( this->main.GetCountTCP() > 0 ){
		EnableWindow(GetDlgItem(IDC_CHECK_TCP), TRUE);
	}else{
		EnableWindow(GetDlgItem(IDC_CHECK_TCP), FALSE);
		Button_SetCheck(GetDlgItem(IDC_CHECK_TCP), BST_UNCHECKED);
	}
	this->main.SendUDP(Button_GetCheck(GetDlgItem(IDC_CHECK_UDP)));
	this->main.SendTCP(Button_GetCheck(GetDlgItem(IDC_CHECK_TCP)));
}

int CEpgDataCap_BonDlg::ReloadServiceList(int selONID, int selTSID, int selSID)
{
	this->serviceList.clear();
	ComboBox_ResetContent(GetDlgItem(IDC_COMBO_SERVICE));

	DWORD ret = this->main.GetServiceList(&this->serviceList);
	if( ret != NO_ERR || this->serviceList.size() == 0 ){
		WCHAR log[512 + 64] = L"";
		GetDlgItemText(m_hWnd, IDC_EDIT_LOG, log, 512);
		wcscat_s(log, L"チャンネル情報の読み込みに失敗しました\r\n");
		SetDlgItemText(m_hWnd, IDC_EDIT_LOG, log);
	}else{
		int selectIndex = -1;
		int selectSel = -1;
		for( size_t i=0; i<this->serviceList.size(); i++ ){
			if( selectIndex < 0 ||
			    (this->serviceList[i].originalNetworkID == selONID &&
			     this->serviceList[i].transportStreamID == selTSID &&
			     this->serviceList[i].serviceID == selSID) ){
				//一覧には表示しないがリストには存在する場合もある
				selectIndex = (int)i;
			}
			if( this->serviceList[i].useViewFlag == TRUE ){
				int index = ComboBox_AddString(GetDlgItem(IDC_COMBO_SERVICE), this->serviceList[i].serviceName.c_str());
				ComboBox_SetItemData(GetDlgItem(IDC_COMBO_SERVICE), index, i);
				if( selectSel < 0 || selectIndex == (int)i ){
					selectSel = index;
				}
			}
		}
		if( selectSel >= 0 ){
			ComboBox_SetCurSel(GetDlgItem(IDC_COMBO_SERVICE), selectSel);
		}
		return selectIndex;
	}
	return -1;
}

BOOL CEpgDataCap_BonDlg::SelectBonDriver(LPCWSTR fileName)
{
	BOOL ret = this->main.OpenBonDriver(fileName);
	if( ret == FALSE ){
		wstring log;
		Format(log, L"BonDriverのオープンができませんでした\r\n%ls\r\n", fileName);
		SetDlgItemText(m_hWnd, IDC_EDIT_LOG, log.c_str());
		BtnUpdate(GUI_OPEN_FAIL);
	}else{
		SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"");
		BtnUpdate(GUI_NORMAL);
	}
	return ret;
}

BOOL CEpgDataCap_BonDlg::SelectService(const CH_DATA4& chData)
{
	return this->main.SetCh(chData.originalNetworkID, chData.transportStreamID, chData.serviceID, chData.space, chData.ch);
}

void CEpgDataCap_BonDlg::OnBnClickedButtonChscan()
{
	// TODO: ここにコントロール通知ハンドラー コードを追加します。
	if( this->main.StartChScan() == FALSE ){
		SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"チャンネルスキャンを開始できませんでした\r\n");
		return;
	}
	SetTimer(TIMER_CHSCAN_STATSU, 1000, NULL);
	BtnUpdate(GUI_CANCEL_ONLY);
}


void CEpgDataCap_BonDlg::OnBnClickedButtonEpg()
{
	// TODO: ここにコントロール通知ハンドラー コードを追加します。
	if( this->main.StartEpgCap() == FALSE ){
		SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"EPG取得を開始できませんでした\r\n");
		return;
	}
	SetTimer(TIMER_EPGCAP_STATSU, 1000, NULL);
	BtnUpdate(GUI_CANCEL_ONLY);
	ChgIconStatus();
}


void CEpgDataCap_BonDlg::OnBnClickedButtonRec()
{
	// TODO: ここにコントロール通知ハンドラー コードを追加します。
	if( this->main.StartRec() != NO_ERR ){
		SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"録画を開始できませんでした\r\n");
		return;
	}
	SYSTEMTIME end;
	ConvertSystemTime(GetNowI64Time() + 30 * 60 * I64_1SEC, &end);

	ComboBox_SetCurSel(GetDlgItem(IDC_COMBO_REC_H), end.wHour);
	ComboBox_SetCurSel(GetDlgItem(IDC_COMBO_REC_M), end.wMinute);

	SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"録画中\r\n");

	BtnUpdate(GUI_REC);
	ChgIconStatus();
}


void CEpgDataCap_BonDlg::OnBnClickedButtonCancel()
{
	// TODO: ここにコントロール通知ハンドラー コードを追加します。
	if( this->main.IsRec() == TRUE ){
		WCHAR caption[128] = L"";
		GetWindowText(m_hWnd, caption, 128);
		disableKeyboardHook = TRUE;
		int result = MessageBox( m_hWnd, L"録画を停止しますか？", caption, MB_YESNO | MB_ICONQUESTION );
		disableKeyboardHook = FALSE;
		if( result == IDNO ){
			return ;
		}
	}
	SetDlgItemText(m_hWnd, IDC_EDIT_LOG, L"キャンセルされました\r\n");

	this->main.StopChScan();
	KillTimer(TIMER_CHSCAN_STATSU);
	this->main.StopEpgCap();
	KillTimer(TIMER_EPGCAP_STATSU);
	this->main.StopRec();
	KillTimer(TIMER_REC_END);
	this->main.StopReserveRec();


	BtnUpdate(GUI_NORMAL);
	ChgIconStatus();
}


void CEpgDataCap_BonDlg::OnBnClickedButtonView()
{
	// TODO: ここにコントロール通知ハンドラー コードを追加します。
	this->main.ViewAppOpen();
}


void CEpgDataCap_BonDlg::OnBnClickedCheckUdp()
{
	// TODO: ここにコントロール通知ハンドラー コードを追加します。
	this->main.SendUDP(Button_GetCheck(GetDlgItem(IDC_CHECK_UDP)));
}


void CEpgDataCap_BonDlg::OnBnClickedCheckTcp()
{
	// TODO: ここにコントロール通知ハンドラー コードを追加します。
	this->main.SendTCP(Button_GetCheck(GetDlgItem(IDC_CHECK_TCP)));
}


void CEpgDataCap_BonDlg::OnBnClickedCheckRecSet()
{
	// TODO: ここにコントロール通知ハンドラー コードを追加します。
	if( Button_GetCheck(GetDlgItem(IDC_CHECK_REC_SET)) != BST_UNCHECKED ){
		BtnUpdate(GUI_REC_SET_TIME);

		int selH = ComboBox_GetCurSel(GetDlgItem(IDC_COMBO_REC_H));
		int selM = ComboBox_GetCurSel(GetDlgItem(IDC_COMBO_REC_M));

		DWORD nowTime = (DWORD)(GetNowI64Time() / I64_1SEC % (24*60*60));
		DWORD endTime = selH*60*60 + selM*60;

		if( nowTime > endTime ){
			endTime += 24*60*60;
		}
		SetTimer(TIMER_REC_END, (endTime-nowTime)*1000, NULL );
	}else{
		BtnUpdate(GUI_REC);
		KillTimer(TIMER_REC_END);
	}
}


void CEpgDataCap_BonDlg::OnBnClickedCheckNextpg()
{
	// TODO: ここにコントロール通知ハンドラー コードを追加します。
	wstring info = L"";
	this->main.GetEpgInfo(Button_GetCheck(GetDlgItem(IDC_CHECK_NEXTPG)), &info);
	WCHAR pgInfo[512] = L"";
	GetDlgItemText(m_hWnd, IDC_EDIT_PG_INFO, pgInfo, 512);
	if( info.substr(0, 511).compare(pgInfo) != 0 ){
		SetDlgItemText(m_hWnd, IDC_EDIT_PG_INFO, info.c_str());
	}
}


BOOL CEpgDataCap_BonDlg::OnQueryEndSession()
{
	// TODO:  ここに特定なクエリの終了セッション コードを追加してください。
	if( this->main.IsRec() == TRUE ){
		ShowWindow(m_hWnd, SW_SHOW);
		return FALSE;
	}
	return TRUE;
}


void CEpgDataCap_BonDlg::OnEndSession(BOOL bEnding)
{
	// TODO: ここにメッセージ ハンドラー コードを追加します。
	if( bEnding == TRUE ){
		if( this->main.IsRec() == TRUE ){
			this->main.StopReserveRec();
			this->main.StopRec();
		}
	}
}


LRESULT CALLBACK CEpgDataCap_BonDlg::KeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
{
	//Enter,Escを無視する
	if( disableKeyboardHook == FALSE && nCode == HC_ACTION && (wParam == VK_RETURN || wParam == VK_ESCAPE) && (lParam & (1 << 30)) == 0 ){
		return TRUE;
	}
	return CallNextHookEx(NULL, nCode, wParam, lParam);
}


INT_PTR CALLBACK CEpgDataCap_BonDlg::DlgProc(HWND hDlg, UINT uMsg, WPARAM wParam, LPARAM lParam)
{
	CEpgDataCap_BonDlg* pSys = (CEpgDataCap_BonDlg*)GetWindowLongPtr(hDlg, GWLP_USERDATA);
	if( pSys == NULL && uMsg != WM_INITDIALOG ){
		return FALSE;
	}
	switch( uMsg ){
	case WM_INITDIALOG:
		SetWindowLongPtr(hDlg, GWLP_USERDATA, lParam);
		pSys = (CEpgDataCap_BonDlg*)lParam;
		pSys->m_hWnd = hDlg;
		pSys->m_hKeyboardHook = SetWindowsHookEx(WH_KEYBOARD, KeyboardProc, NULL, GetCurrentThreadId());
		return pSys->OnInitDialog();
	case WM_NCDESTROY:
		UnhookWindowsHookEx(pSys->m_hKeyboardHook);
		pSys->m_hWnd = NULL;
		break;
	case WM_DESTROY:
		pSys->OnDestroy();
		break;
	case WM_TIMER:
		pSys->OnTimer(wParam);
		break;
	case WM_SIZE:
		pSys->OnSize((UINT)wParam, LOWORD(lParam), HIWORD(lParam));
		break;
	case WM_QUERYENDSESSION:
		SetWindowLongPtr(hDlg, DWLP_MSGRESULT, pSys->OnQueryEndSession());
		return TRUE;
	case WM_ENDSESSION:
		pSys->OnEndSession((BOOL)wParam);
		break;
	case WM_COMMAND:
		switch( LOWORD(wParam) ){
		case IDC_COMBO_TUNER:
			if( HIWORD(wParam) == CBN_SELCHANGE ){
				pSys->OnCbnSelchangeComboTuner();
			}
			break;
		case IDC_COMBO_SERVICE:
			if( HIWORD(wParam) == CBN_SELCHANGE ){
				pSys->OnCbnSelchangeComboService();
			}
			break;
		case IDC_BUTTON_SET:
			pSys->OnBnClickedButtonSet();
			break;
		case IDC_BUTTON_CHSCAN:
			pSys->OnBnClickedButtonChscan();
			break;
		case IDC_BUTTON_EPG:
			pSys->OnBnClickedButtonEpg();
			break;
		case IDC_BUTTON_REC:
			pSys->OnBnClickedButtonRec();
			break;
		case IDC_CHECK_REC_SET:
			pSys->OnBnClickedCheckRecSet();
			break;
		case IDC_BUTTON_CANCEL:
			pSys->OnBnClickedButtonCancel();
			break;
		case IDC_CHECK_TCP:
			pSys->OnBnClickedCheckTcp();
			break;
		case IDC_CHECK_UDP:
			pSys->OnBnClickedCheckUdp();
			break;
		case IDC_BUTTON_VIEW:
			pSys->OnBnClickedButtonView();
			break;
		case IDC_CHECK_NEXTPG:
			pSys->OnBnClickedCheckNextpg();
			break;
		case IDOK:
		case IDCANCEL:
			//デッドロック回避のためメッセージポンプを維持しつつサーバを終わらせる
			pSys->main.StopServer(true);
			pSys->SetTimer(TIMER_TRY_STOP_SERVER, 20, NULL);
			SetWindowLongPtr(hDlg, DWLP_MSGRESULT, 0);
			return TRUE;
		}
		break;
	case WM_SYSCOMMAND:
		{
			BOOL bProcessed = FALSE;
			pSys->OnSysCommand((UINT)(wParam & 0xFFF0), lParam, &bProcessed);
			if( bProcessed != FALSE ){
				SetWindowLongPtr(hDlg, DWLP_MSGRESULT, 0);
				return TRUE;
			}
		}
		break;
	default:
		if( uMsg == taskbarCreated ){
			pSys->OnTaskbarCreated(wParam, lParam);
		}else if( uMsg >= WM_USER ){
			pSys->WindowProc(uMsg, wParam, lParam);
		}
		break;
	}
	return FALSE;
}
