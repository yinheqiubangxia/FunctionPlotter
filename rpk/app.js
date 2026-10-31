App({
  globalData: {
    // 全局主题：light / dark，默认跟随系统
    theme: 'dark'
  },

  onCreate() {
    // 手环端默认深色（省电、OLED 更友好）；浅色主题由设置页切换时写入 globalData
  },

  onDestroy() {}
})
