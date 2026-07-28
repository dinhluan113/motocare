use tauri::{AppHandle, Manager};
use tauri_plugin_updater::UpdaterExt;

fn set_splash_status(app: &AppHandle, message: &str, progress: Option<u8>) {
    let Some(window) = app.get_webview_window("splashscreen") else {
        return;
    };

    let payload = serde_json::json!({
        "message": message,
        "progress": progress
    });
    let _ = window.eval(&format!("window.setSplashStatus?.({payload});"));
}

fn show_main_window(app: &AppHandle) {
    if let Some(main) = app.get_webview_window("main") {
        let _ = main.show();
        let _ = main.set_focus();
    }

    if let Some(splash) = app.get_webview_window("splashscreen") {
        let _ = splash.close();
    }
}

async fn check_for_updates(app: AppHandle) {
    if cfg!(debug_assertions) {
        set_splash_status(&app, "Đang tải ứng dụng...", None);
        show_main_window(&app);
        return;
    }

    set_splash_status(&app, "Đang kiểm tra phiên bản mới...", None);

    let updater = match app.updater() {
        Ok(updater) => updater,
        Err(error) => {
            eprintln!("Không thể khởi tạo updater: {error}");
            show_main_window(&app);
            return;
        }
    };

    let update = match updater.check().await {
        Ok(update) => update,
        Err(error) => {
            eprintln!("Không thể kiểm tra cập nhật: {error}");
            set_splash_status(&app, "Không thể kiểm tra cập nhật. Đang mở ứng dụng...", None);
            show_main_window(&app);
            return;
        }
    };

    let Some(update) = update else {
        set_splash_status(&app, "Bạn đang dùng phiên bản mới nhất.", Some(100));
        show_main_window(&app);
        return;
    };

    let version = update.version.clone();
    set_splash_status(
        &app,
        &format!("Đã tìm thấy phiên bản {version}. Đang tải xuống..."),
        Some(0),
    );

    let progress_app = app.clone();
    let finish_app = app.clone();
    let progress_version = version.clone();
    let mut downloaded = 0_u64;

    let result = update
        .download_and_install(
            move |chunk_length, content_length| {
                downloaded += chunk_length as u64;
                let progress = content_length
                    .filter(|total| *total > 0)
                    .map(|total| ((downloaded.saturating_mul(100) / total).min(100)) as u8);

                set_splash_status(
                    &progress_app,
                    &format!("Đang tải phiên bản {progress_version}..."),
                    progress,
                );
            },
            move || {
                set_splash_status(&finish_app, "Đã tải xong. Đang cài đặt...", Some(100));
            },
        )
        .await;

    match result {
        Ok(()) => {
            set_splash_status(&app, "Cập nhật hoàn tất. Đang khởi động lại...", Some(100));
            app.restart();
        }
        Err(error) => {
            eprintln!("Không thể cài đặt cập nhật: {error}");
            set_splash_status(&app, "Cập nhật thất bại. Đang mở phiên bản hiện tại...", None);
            show_main_window(&app);
        }
    }
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_updater::Builder::new().build())
        .setup(|app| {
            tauri::async_runtime::spawn(check_for_updates(app.handle().clone()));
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running MotoCare");
}
