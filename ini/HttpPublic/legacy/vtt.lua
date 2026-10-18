-- TSファイルの字幕(tsreadexのトレース出力によるもの)をWebVTT形式で取得する
dofile(mg.script_name:gsub('[^\\/]*$','')..'util.lua')

-- 以下、b24tovttのコードをほぼそのまま移植。`tsreadex -n -1 -r - foo.ts | b24tovtt -t nobom-vlc -`に相当

function ProcessFormatVttCue(staMsec,endMsec,group0,groupN)
  if not (0<=staMsec and staMsec<360000000 and
          0<=endMsec and endMsec<360000000 and
          staMsec<=endMsec) then
    return nil
  end
  local cue=('%02d:%02d:%02d.%03d --> %02d:%02d:%02d.%03d\n<v %s><c>%s</c></v>\n<v %s><c>'):format(
    math.floor(staMsec/3600000),math.floor(staMsec/60000)%60,math.floor(staMsec/1000)%60,staMsec%1000,
    math.floor(endMsec/3600000),math.floor(endMsec/60000)%60,math.floor(endMsec/1000)%60,endMsec%1000,
    group0:sub(1,11),
    group0:sub(13):gsub('&','&amp;'):gsub('<','&lt;'):gsub('>','&gt;'),
    groupN:sub(1,11))
  local i=#cue+1
  cue=cue..groupN:sub(13):gsub('&','&amp;'):gsub('<','&lt;'):gsub('>','&gt;')

  -- Insert tags before and after the readable text.
  local isIn=true
  while true do
    i=cue:find('\n',i)
    if not i then break end
    cue=cue:sub(0,i-1)..(isIn and '</c>' or '<c>')..cue:sub(i+1)
    isIn=not isIn
  end
  if isIn then
    cue=cue..'</c>'
  end
  cue=cue..'</v>\n'
  return cue
end

delayMsec=-300
initialPcr=nil
group0=nil
groupN=nil
lastPts=0

function Write(ptsOrFinal)
  if group0 and groupN then
    local staMsec=math.floor((0x200000000+lastPts-initialPcr)%0x200000000/90)+delayMsec
    local endMsec=ptsOrFinal and math.floor((0x200000000+ptsOrFinal-initialPcr)%0x200000000/90)+delayMsec or staMsec+5000
    local cue=ProcessFormatVttCue(staMsec,endMsec,group0,groupN)
    if cue and not mg.write('\n'..cue) then return false end
  end
  return true
end

lineBuf=''
nextGroup0=nil
lastNotePts=0

function Convert(s)
  lineBuf=lineBuf..s
  while true do
    local line=lineBuf:match('^([^\n]*)\n')
    if not line then break end
    lineBuf=lineBuf:sub(#line+2)

    if not initialPcr then
      if #line>=28 and line:find('^pcrpid=') and line:find('^;pcr=',14) then
        initialPcr=tonumber(line:match('^[0-9]*',19))
      end
    elseif #line>=43 and line:find('^pts=') and line:find('^;pcrrel=',15) then
      local i=line:find(';b24caption')
      if i and i<(line:find(';b24superimpose') or math.huge) then
        local pts=tonumber(line:match('^[0-9]*',5)) or 0
        if line:find('^0=',i+11) then
          -- Caption management data
          if pts-lastNotePts>5000000 then
            -- 進捗表示用のNOTE
            if not mg.write(('\nNOTE msec=%08d\n'):format(math.floor((0x200000000+pts-initialPcr)%0x200000000/90)+delayMsec)) then return false end
            lastNotePts=pts
          end
          nextGroup0=line:sub(i+1)
        elseif line:find('^1=',i+11) then
          -- Caption data
          if not Write(pts) then return false end
          group0=nextGroup0
          groupN=line:sub(i+1)
          lastPts=pts

          local paramPos=line:find(';text=')
          if paramPos and paramPos<i then
            -- Insert '\n' before and after the readable text.
            local byteCount=13
            paramPos=paramPos+5
            while true do
              local codeCount=line:match('^[=,]([0-9]*)',paramPos)
              if not codeCount then break end
              paramPos=paramPos+1+#codeCount
              codeCount=tonumber(codeCount) or 0
              while codeCount>0 and byteCount<#groupN do
                byteCount=byteCount+1
                if byteCount==#groupN or groupN:byte(byteCount)<0x80 or groupN:byte(byteCount)>=0xC0 then
                  codeCount=codeCount-1
                end
              end
              if codeCount~=0 then break end
              groupN=groupN:sub(1,byteCount-1)..'\n'..groupN:sub(byteCount)
              byteCount=byteCount+1
            end
          end
        end
      end
    end
  end
  return true
end

styleTemplateVlc='\nSTYLE\n::cue(c){font-size:0.001em;color:rgba(0,0,0,0);visibility:hidden}\n'
b24CaptionMagic='b24caption-2aaf6fcf-6388-4e59-88ff-46e1555d0edd'

fpath=mg.get_var(mg.request_info.query_string,'fname')
if fpath then
  fname=mg.md5(fpath:upper()):sub(27)..'.vtt'
  fpath=DocumentToNativePath(fpath)
end

code=404
if fpath then
  ext=fpath:match('%.[0-9A-Za-z]+$') or ''
  extts=edcb.GetPrivateProfile('SET','TSExt','.ts','EpgTimerSrv.ini')
  -- 拡張子を限定
  if IsEqualPath(ext,extts) and EdcbFindFilePlain(fpath) then
    code=503
    -- 高負荷なので複数実行はしない
    if WIN32 and edcb.os.execute('powershell -NoProfile -c "try{exit @(gwmi win32_process -filter \\"name=\'tsreadex.exe\' and commandline like \'% -z edcb-vtt-legacy %\'\\").count}catch{exit 1}"') or
       not WIN32 and edcb.os.execute('pgrep -xf "tsreadex -z edcb-vtt-legacy .*" >/dev/null ; [ $? -eq 1 ]') then
      code=500
      tsreadex=FindToolsCommand('tsreadex')
      -- "-c 5"は進捗表示用のNOTEを送るため
      cmd=tsreadex..' -z edcb-vtt-legacy -n -1 -c 5 -r - '..QuoteCommandArgForPath(fpath)
      f=edcb.io.popen(WIN32 and '"'..cmd..'"' or cmd)
      if f then
        while true do
          s=f:read(64) or ''
          if #s==0 then
            -- ファイナライズ
            Write()
            break
          end
          if code~=200 then
            code=200
            -- 高コストなので有効期限を長くする
            if not mg.write(Response(code,mg.get_mime_type(fname),'utf-8',nil,nil,86400*7)..'Content-Disposition: filename='..fname..'\r\n\r\n') or
               mg.request_info.request_method=='HEAD' or
               not mg.write('WEBVTT\n\nNOTE '..b24CaptionMagic..'\n'..styleTemplateVlc) then
              TerminateCommandlineLike('tsreadex',' -z edcb-vtt-legacy ')
              break
            end
          end
          if not Convert(s) then
            TerminateCommandlineLike('tsreadex',' -z edcb-vtt-legacy ')
            break
          end
        end
        f:close()
      end
    end
  end
end

if code~=200 then
  mg.write(Response(code,nil,nil,0)..'\r\n')
end
