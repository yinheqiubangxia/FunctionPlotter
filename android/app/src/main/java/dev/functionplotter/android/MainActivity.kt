package dev.functionplotter.android

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.BackHandler
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.activity.viewModels
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.slideInVertically
import androidx.compose.animation.slideOutVertically
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyRow
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material.icons.outlined.AutoAwesome
import androidx.compose.material.icons.outlined.GridOn
import androidx.compose.material3.AssistChip
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilledTonalIconButton
import androidx.compose.material3.FilterChip
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.InputChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.unit.dp
import dev.functionplotter.android.ai.AiProviders
import dev.functionplotter.android.ai.AiViewModel
import dev.functionplotter.android.plot.PlotCanvas
import dev.functionplotter.android.plot.PlotViewModel
import dev.functionplotter.android.ui.theme.FunctionPlotterTheme
import kotlinx.coroutines.launch

class MainActivity : ComponentActivity() {

    private val plotViewModel: PlotViewModel by viewModels()
    private val aiViewModel: AiViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        // 边到边布局 + 预测式返回手势（清单已开 enableOnBackPressedCallback，
        // Compose BackHandler 自动接入 OnBackPressedDispatcher）
        enableEdgeToEdge()
        setContent {
            FunctionPlotterTheme {
                AppRoot(plotViewModel, aiViewModel)
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AppRoot(plotVm: PlotViewModel, aiVm: AiViewModel) {
    val plotState by plotVm.uiState.collectAsState()
    val aiState by aiVm.uiState.collectAsState()

    var showAiSheet by remember { mutableStateOf(false) }
    var showSettingsSheet by remember { mutableStateOf(false) }
    val snackbar = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()

    // 预测式返回：打开面板时返回键先关闭面板，手势过程中有系统级预览动画
    BackHandler(enabled = showAiSheet || showSettingsSheet) {
        showAiSheet = false
        showSettingsSheet = false
        plotVm.setPreviewExpression(null)
    }

    LaunchedEffect(plotState.inputError) {
        plotState.inputError?.let {
            snackbar.showSnackbar(it)
            plotVm.clearInputError()
        }
    }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbar) },
        floatingActionButton = {
            Column(horizontalAlignment = Alignment.End, verticalArrangement = Arrangement.spacedBy(12.dp)) {
                FloatingActionButton(onClick = { showAiSheet = true }) {
                    Icon(Icons.Outlined.AutoAwesome, contentDescription = "AI 辅助绘图")
                }
            }
        },
    ) { padding ->
        Box(
            Modifier
                .fillMaxSize()
                .padding(padding)
        ) {
            PlotCanvas(
                state = plotState,
                onPan = plotVm::pan,
                onZoom = plotVm::zoom,
                onGestureEnd = { plotVm.resample() },
                onDoubleTap = plotVm::resetViewport,
                modifier = Modifier.fillMaxSize(),
            )

            // 顶部工具条：网格/坐标轴开关 + 复位
            Row(
                Modifier
                    .align(Alignment.TopEnd)
                    .padding(12.dp),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                FilledTonalIconButton(onClick = plotVm::toggleGrid) {
                    Icon(
                        Icons.Outlined.GridOn,
                        contentDescription = "网格开关",
                        tint = if (plotState.showGrid) MaterialTheme.colorScheme.primary
                        else MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                FilledTonalIconButton(onClick = plotVm::toggleAxes) {
                    Text(
                        "xy",
                        style = MaterialTheme.typography.labelLarge,
                        color = if (plotState.showAxes) MaterialTheme.colorScheme.primary
                        else MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                FilledTonalIconButton(onClick = plotVm::resetViewport) {
                    Icon(Icons.Default.Refresh, contentDescription = "复位视图")
                }
            }

            // 底部：表达式输入 + 已添加曲线 chips
            Surface(
                Modifier
                    .align(Alignment.BottomCenter)
                    .imePadding(),
                color = MaterialTheme.colorScheme.surface.copy(alpha = 0.94f),
                tonalElevation = 3.dp,
            ) {
                Column(Modifier.padding(horizontal = 16.dp, vertical = 10.dp)) {
                    ExpressionInput(onAdd = plotVm::addFunction)
                    if (plotState.functions.isNotEmpty()) {
                        Spacer(Modifier.height(8.dp))
                        LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            items(plotState.functions, key = { it.id }) { fn ->
                                InputChip(
                                    selected = true,
                                    onClick = { plotVm.removeFunction(fn.id) },
                                    label = { Text("y = ${fn.expression}") },
                                    leadingIcon = {
                                        Box(
                                            Modifier
                                                .size(12.dp)
                                                .background(fn.color, RoundedCornerShape(6.dp))
                                        )
                                    },
                                    trailingIcon = {
                                        Icon(Icons.Default.Close, contentDescription = "删除", Modifier.size(16.dp))
                                    },
                                )
                            }
                        }
                    }
                }
            }
        }
    }

    if (showAiSheet) {
        ModalBottomSheet(
            onDismissRequest = {
                showAiSheet = false
                plotVm.setPreviewExpression(null)
            },
            sheetState = rememberModalBottomSheetState(skipPartiallyExpanded = true),
        ) {
            AiPanel(
                aiState = aiState,
                onAsk = { prompt -> aiVm.ask(prompt, plotVm::setPreviewExpression) },
                onSelect = { idx -> aiVm.selectSuggestion(idx, plotVm::setPreviewExpression) },
                onAccept = {
                    plotVm.acceptPreview()
                    aiVm.clearResult()
                    showAiSheet = false
                },
                onOpenSettings = { showSettingsSheet = true },
            )
        }
    }

    if (showSettingsSheet) {
        ModalBottomSheet(onDismissRequest = { showSettingsSheet = false }) {
            AiSettingsPanel(
                aiState = aiState,
                onSave = { aiVm.saveSettings(it); showSettingsSheet = false },
            )
        }
    }
}

@Composable
private fun ExpressionInput(onAdd: (String) -> Boolean) {
    var text by remember { mutableStateOf("") }
    val submit = {
        if (text.isNotBlank() && onAdd(text)) text = ""
    }
    OutlinedTextField(
        value = text,
        onValueChange = { text = it },
        modifier = Modifier.fillMaxWidth(),
        placeholder = { Text("输入表达式，如 sin(x) + x^2 / 4") },
        singleLine = true,
        shape = RoundedCornerShape(16.dp),
        keyboardOptions = KeyboardOptions(imeAction = ImeAction.Done),
        keyboardActions = KeyboardActions(onDone = { submit() }),
        trailingIcon = {
            IconButton(onClick = { submit() }) {
                Icon(Icons.Default.Add, contentDescription = "添加曲线")
            }
        },
    )
}

@Composable
private fun AiPanel(
    aiState: dev.functionplotter.android.ai.AiUiState,
    onAsk: (String) -> Unit,
    onSelect: (Int) -> Unit,
    onAccept: () -> Unit,
    onOpenSettings: () -> Unit,
) {
    var prompt by remember { mutableStateOf("") }

    Column(
        Modifier
            .padding(horizontal = 20.dp)
            .padding(bottom = 32.dp)
            .verticalScroll(rememberScrollState()),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text("AI 辅助绘图", style = MaterialTheme.typography.titleLarge, modifier = Modifier.weight(1f))
            IconButton(onClick = onOpenSettings) {
                Icon(Icons.Default.Settings, contentDescription = "AI 设置")
            }
        }
        Text(
            "当前服务商：${AiProviders.byId(aiState.settings.providerId).displayName}",
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Spacer(Modifier.height(12.dp))
        OutlinedTextField(
            value = prompt,
            onValueChange = { prompt = it },
            modifier = Modifier.fillMaxWidth(),
            placeholder = { Text("描述你想画的函数，如：画个心形线") },
            minLines = 2,
            shape = RoundedCornerShape(16.dp),
        )
        Spacer(Modifier.height(12.dp))
        Button(
            onClick = { onAsk(prompt) },
            enabled = !aiState.loading && prompt.isNotBlank(),
            modifier = Modifier.fillMaxWidth(),
        ) {
            if (aiState.loading) {
                CircularProgressIndicator(Modifier.size(18.dp), strokeWidth = 2.dp)
                Spacer(Modifier.size(8.dp))
            }
            Text(if (aiState.loading) "生成中…" else "生成表达式")
        }

        aiState.error?.let {
            Spacer(Modifier.height(12.dp))
            Text(it, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall)
        }

        if (aiState.suggestions.isNotEmpty()) {
            Spacer(Modifier.height(16.dp))
            Text("点击建议可在画布上实时预览", style = MaterialTheme.typography.labelMedium)
            Spacer(Modifier.height(8.dp))
            aiState.suggestions.forEachIndexed { idx, expr ->
                FilterChip(
                    selected = aiState.selectedIndex == idx,
                    onClick = { onSelect(idx) },
                    label = { Text("y = $expr") },
                    modifier = Modifier.padding(end = 8.dp, bottom = 8.dp),
                )
            }
            aiState.note?.let {
                Spacer(Modifier.height(4.dp))
                Text(it, style = MaterialTheme.typography.bodySmall)
            }
            Spacer(Modifier.height(12.dp))
            Button(onClick = onAccept, modifier = Modifier.fillMaxWidth()) {
                Text("添加到画布")
            }
        }
    }
}

@Composable
private fun AiSettingsPanel(
    aiState: dev.functionplotter.android.ai.AiUiState,
    onSave: (dev.functionplotter.android.ai.AiSettings) -> Unit,
) {
    var providerId by remember { mutableStateOf(aiState.settings.providerId) }
    var apiKey by remember { mutableStateOf(aiState.settings.apiKey) }
    var baseUrl by remember { mutableStateOf(aiState.settings.baseUrl) }
    var model by remember { mutableStateOf(aiState.settings.model) }
    val provider = AiProviders.byId(providerId)

    Column(
        Modifier
            .padding(horizontal = 20.dp)
            .padding(bottom = 32.dp)
            .verticalScroll(rememberScrollState()),
    ) {
        Text("AI 服务商设置", style = MaterialTheme.typography.titleLarge)
        Spacer(Modifier.height(12.dp))
        LazyRow(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
            items(AiProviders.ALL) { p ->
                FilterChip(
                    selected = p.id == providerId,
                    onClick = {
                        providerId = p.id
                        if (p.id != AiProviders.CUSTOM.id) {
                            baseUrl = p.baseUrl
                            model = p.defaultModel
                        }
                    },
                    label = { Text(p.displayName) },
                )
            }
        }
        Spacer(Modifier.height(12.dp))
        OutlinedTextField(
            value = apiKey, onValueChange = { apiKey = it },
            label = { Text("API Key") }, placeholder = { Text(provider.keyHint) },
            singleLine = true, modifier = Modifier.fillMaxWidth(),
        )
        Spacer(Modifier.height(8.dp))
        OutlinedTextField(
            value = baseUrl, onValueChange = { baseUrl = it },
            label = { Text("Base URL") }, singleLine = true,
            enabled = providerId == AiProviders.CUSTOM.id,
            modifier = Modifier.fillMaxWidth(),
        )
        Spacer(Modifier.height(8.dp))
        OutlinedTextField(
            value = model, onValueChange = { model = it },
            label = { Text("模型") }, singleLine = true,
            modifier = Modifier.fillMaxWidth(),
        )
        Spacer(Modifier.height(16.dp))
        Button(
            onClick = {
                onSave(aiState.settings.copy(providerId = providerId, apiKey = apiKey, baseUrl = baseUrl, model = model))
            },
            modifier = Modifier.fillMaxWidth(),
        ) { Text("保存") }
    }
}
