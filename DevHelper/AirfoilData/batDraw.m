%% ==============================
% 批处理 CL / CD 曲线生成脚本
% ===============================

clc; clear;

%% 配置输入文件与输出目录
inputFile = "Airfoils.txt";  % 改为你的文件路径
outputFolder = "curves_output";
if ~exist(outputFolder, 'dir')
    mkdir(outputFolder);
end

%% 读取文件
raw = fileread(inputFile);
lines = splitlines(raw);

%% 初始化
dataList = struct([]);
idx = 0;
mode = "";  % 当前 LIFT / DRAG
tempLift = [];
tempDrag = [];
preWrap = "Clamp";
postWrap = "Clamp";

%% 逐行解析 TXT 文件
for i = 1:length(lines)
    line = strtrim(lines{i});
    if line == ""; continue; end

    % 新翼面开始
    if startsWith(line, "[id")
        % 保存上一个翼面数据（如果存在）
        if idx > 0
            dataList(idx).LIFT = tempLift;
            dataList(idx).DRAG = tempDrag;
            dataList(idx).preWrapMode = preWrap;
            dataList(idx).postWrapMode = postWrap;
        end

        % 新翼面
        idx = idx + 1;
        tokens = regexp(line, '\[id:\s*(\d+)\s*:\s*(.+)\]', 'tokens');
        dataList(idx).id = str2double(tokens{1}{1});
        dataList(idx).name = tokens{1}{2};

        % 重置
        tempLift = [];
        tempDrag = [];
        preWrap = "Clamp";
        postWrap = "Clamp";
        mode = "";
        continue;
    end

    % 检测 LIFT / DRAG 块
    if strcmp(line, "LIFT:"); mode = "LIFT"; continue; end
    if strcmp(line, "DRAG:"); mode = "DRAG"; continue; end

    % WrapMode
    if startsWith(line, "PreWrapMode:")
        tokens = split(line, ":");
        preWrap = strtrim(tokens{2});
        continue;
    elseif startsWith(line, "PostWrapMode:")
        tokens = split(line, ":");
        postWrap = strtrim(tokens{2});
        continue;
    end

    % 数值行
    if contains(line, ",")
        nums = str2num(strrep(line, ";", "")); %#ok<ST2NM>
        if strcmp(mode, "LIFT")
            tempLift = [tempLift; nums];
        elseif strcmp(mode, "DRAG")
            tempDrag = [tempDrag; nums];
        end
    end
end

% 保存最后一个翼面数据
if idx > 0
    dataList(idx).LIFT = tempLift;
    dataList(idx).DRAG = tempDrag;
    dataList(idx).preWrapMode = preWrap;
    dataList(idx).postWrapMode = postWrap;
end

%% ==============================
% 生成曲线并保存 PNG
%% ==============================
for k = 1:length(dataList)
    wingName = dataList(k).name;
    fprintf("生成曲线：%s\n", wingName);
    fprintf("  PreWrapMode: %s, PostWrapMode: %s\n", ...
        dataList(k).preWrapMode, dataList(k).postWrapMode);

    % --- LIFT ---
    mat = dataList(k).LIFT;
    if isempty(mat)
        warning("翼面 %s LIFT 数据为空", wingName);
    else
        t = mat(:,1);   % 横轴（可负）
        v = mat(:,2);   % 值
        [t_plot, v_plot] = hermiteInterp(t, v);

        figure; plot(t_plot, v_plot, 'LineWidth', 2); grid on; hold on;
        plot(t, v, 'ro', 'MarkerFaceColor','r'); % 原始键值
        title([wingName ' - LIFT ' dataList(k).preWrapMode ', ' dataList(k).postWrapMode], 'Interpreter', 'none');
        xlabel('AoA'); ylabel('CL');
        saveas(gcf, fullfile(outputFolder, [wingName '_LIFT.png']));
        close;
    end

    % --- DRAG ---
    mat = dataList(k).DRAG;
    if isempty(mat)
        warning("翼面 %s DRAG 数据为空", wingName);
    else
        t = mat(:,1);
        v = mat(:,2);
        [t_plot, v_plot] = hermiteInterp(t, v);

        figure; plot(t_plot, v_plot, 'LineWidth', 2); grid on; hold on;
        plot(t, v, 'ro', 'MarkerFaceColor','r'); % 原始键值
        title([wingName ' - DRAG ' dataList(k).preWrapMode ', ' dataList(k).postWrapMode], 'Interpreter', 'none');
        xlabel('AoA'); ylabel('CD');
        saveas(gcf, fullfile(outputFolder, [wingName '_DRAG.png']));
        close;
    end
end

disp("全部曲线生成完成。");

%% =========================================
% 局部函数 Hermite 插值
%% =========================================
function [t_plot, v_plot] = hermiteInterp(t, v)
    n = length(v);
    if length(t) ~= n
        error('横轴 t 与值 v 长度不一致');
    end

    % 导数（斜率）按真实横轴计算
    inSlope  = gradient(v) ./ gradient(t);
    outSlope = inSlope;
    inWeight  = ones(n,1);
    outWeight = ones(n,1);

    t_plot = [];
    v_plot = [];

    for i = 1:(n-1)
        t_segment = linspace(t(i), t(i+1), 100);
        h = t(i+1) - t(i);
        s = (t_segment - t(i)) / h;

        h00 = 2*s.^3 - 3*s.^2 + 1;
        h10 = s.^3 - 2*s.^2 + s;
        h01 = -2*s.^3 + 3*s.^2;
        h11 = s.^3 - s.^2;

        m0 = outSlope(i) * outWeight(i);
        m1 = inSlope(i+1) * inWeight(i+1);

        v_segment = h00*v(i) + h10*h*m0 + h01*v(i+1) + h11*h*m1;

        t_plot = [t_plot, t_segment];
        v_plot = [v_plot, v_segment];
    end
end
