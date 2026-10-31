package dev.functionplotter.android.ui.theme

import android.os.Build
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext

// 备用品牌色（Android 12 以下没有动态配色时使用）
private val LightColors = lightColorScheme(
    primary = Color(0xFF6D4BD8),
    secondary = Color(0xFF00696D),
    tertiary = Color(0xFF9A4520),
)
private val DarkColors = darkColorScheme(
    primary = Color(0xFFCFBDFE),
    secondary = Color(0xFF4CD9DE),
    tertiary = Color(0xFFFFB59B),
)

/**
 * Material Design 3 主题：
 * - Android 12+ 启用动态配色（Material You，跟随壁纸）
 * - 明暗主题跟随系统，可被 [darkTheme] 显式覆盖
 */
@Composable
fun FunctionPlotterTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    dynamicColor: Boolean = true,
    content: @Composable () -> Unit,
) {
    val colorScheme = when {
        dynamicColor && Build.VERSION.SDK_INT >= Build.VERSION_CODES.S -> {
            val context = LocalContext.current
            if (darkTheme) dynamicDarkColorScheme(context) else dynamicLightColorScheme(context)
        }
        darkTheme -> DarkColors
        else -> LightColors
    }
    MaterialTheme(
        colorScheme = colorScheme,
        content = content,
    )
}
